#!/bin/sh
#
# Creates a short-lived Scaleway Public Gateway + SSH bastion attached to the
# shared private network, opens a local port-forward through it to the DB, runs
# whatever command it's given against that tunnel, then tears the gateway down
# again.
#
# The purpose is to allow the CD workflow runner to run database migrations against the
# DB that lives on a private network. This pattern was chosen, since Scaleways Serverless
# Jobs do not yet support being attached to private networks, and the DB should not be exposed
# to the public internet. Once Scaleways Serverless Jobs support private networks, this script
# can be removed and the migrations can be run by a Serverless Job attached to the private network.
#
# Usage:
#   with-bastion-tunnel.sh -- <command> [args...]
#
# Examples:
# ./with-bastion-tunnel.sh -- psql -h 127.0.0.1 --port "$LOCAL_PORT" -d "$DB_NAME" -U "$DB_USER"
#
# ./with-bastion-tunnel.sh -- \
# docker run --rm --network host \
#   -e ASPNETCORE_ENVIRONMENT=Production \
#   -e ConnectionStrings__TodoDb="Host=127.0.0.1;Port=$LOCAL_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD;SSL Mode=Require;Maximum Pool Size=10" \
#   $APP_IMAGE --migrate
#
# Requires a `scw` new enough to speak Public Gateways API v2.
#
# Required environment variables:
#   SCW_PRIVATE_NETWORK_ID        ID of the private network the DB and gateway attach to
#   DB_PRIVATE_IP                 Private IP of the DB instance to reach through the tunnel
#   BASTION_SSH_PRIVATE_KEY_FILE  Path to the private key file matching an SSH key that was
#                                 already registered on the Scaleway Project *before* this
#                                 script runs (keys are copied to the bastion at the moment
#                                 the bastion is enabled, i.e. at gateway creation)
#   SCW_ZONE                      AZ to create the gateway in. MUST match the AZ of the
#                                 private network. Falls back to SCW_DEFAULT_ZONE.
#
# Optional environment variables:
#   DB_PORT               Defaults to 5432
#   LOCAL_PORT            Defaults to 15432 — <command> should connect to 127.0.0.1:$LOCAL_PORT
#   GATEWAY_TYPE          Defaults to VPC-GW-S
#   BASTION_PORT          Defaults to 61000
#   BASTION_ALLOWED_IPS   Comma-separated CIDRs allowed to reach the bastion, e.g.
#                         "203.0.113.7/32". If unset, the bastion is reachable from
#                         0.0.0.0/0 for the lifetime of the run (a valid project SSH key
#                         is still required) and the script warns about it.
#   GATEWAY_TAG           Tag applied to the gateway so orphans can be swept. Default
#                         ci-ephemeral-bastion. See the sweeper note at the bottom.

set -eu

: "${SCW_PRIVATE_NETWORK_ID:?SCW_PRIVATE_NETWORK_ID must be set}"
: "${DB_PRIVATE_IP:?DB_PRIVATE_IP must be set}"
: "${BASTION_SSH_PRIVATE_KEY_FILE:?BASTION_SSH_PRIVATE_KEY_FILE must be set}"

# The gateway must live in the same AZ as the private network. `scw` silently
# defaults to fr-par-1, so make the zone explicit rather than inheriting
# whatever happens to be in the runner's scw config.
SCW_ZONE="${SCW_ZONE:-${SCW_DEFAULT_ZONE:-}}"
if [ -z "$SCW_ZONE" ]; then
  echo "SCW_ZONE (or SCW_DEFAULT_ZONE) must be set to the AZ of $SCW_PRIVATE_NETWORK_ID." >&2
  exit 2
fi

DB_PORT="${DB_PORT:-5432}"
LOCAL_PORT="${LOCAL_PORT:-15432}"
GATEWAY_TYPE="${GATEWAY_TYPE:-VPC-GW-S}"
BASTION_PORT="${BASTION_PORT:-61000}"
BASTION_ALLOWED_IPS="${BASTION_ALLOWED_IPS:-}"
GATEWAY_TAG="${GATEWAY_TAG:-ci-ephemeral-bastion}"

if [ "${1:-}" != "--" ]; then
  echo "Usage: $0 -- <command> [args...]" >&2
  exit 2
fi
shift
if [ "$#" -eq 0 ]; then
  echo "No command given to run once the tunnel is up." >&2
  exit 2
fi

for bin in scw jq ssh ssh-keyscan; do
  if ! command -v "$bin" >/dev/null 2>&1; then
    echo "Required command '$bin' not found on PATH." >&2
    exit 127
  fi
done
if [ -n "$BASTION_ALLOWED_IPS" ] && ! command -v curl >/dev/null 2>&1; then
  echo "BASTION_ALLOWED_IPS is set, which needs 'curl' (see restrict_bastion_ips)." >&2
  exit 127
fi

if [ ! -r "$BASTION_SSH_PRIVATE_KEY_FILE" ]; then
  echo "Cannot read SSH key file $BASTION_SSH_PRIVATE_KEY_FILE." >&2
  exit 2
fi

GATEWAY_ID=""
GATEWAY_NETWORK_ID=""
SSH_PID=""
KNOWN_HOSTS_FILE=""
KEY_FILE=""

cleanup() {
  # Runs on EXIT no matter how the script leaves (success, failed migration,
  # Ctrl-C, killed job) - this is what stops a failed run leaving behind a gateway.
  status=$?
  trap - EXIT INT TERM
  set +e

  if [ -n "$SSH_PID" ]; then
    kill "$SSH_PID" 2>/dev/null
    wait "$SSH_PID" 2>/dev/null
  fi

  # scw dumps the whole resource on a successful delete. Capture that output and
  # only replay it when a call fails - on the happy path the teardown is two lines,
  # and a failure still gets the full API response next to the warning.
  if [ -n "$GATEWAY_NETWORK_ID" ] || [ -n "$GATEWAY_ID" ]; then
    echo "Tearing down bastion gateway ${GATEWAY_ID:-$GATEWAY_NETWORK_ID}..." >&2
  fi

  if [ -n "$GATEWAY_NETWORK_ID" ]; then
    if scw_out=$(scw vpc-gw gateway-network delete "$GATEWAY_NETWORK_ID" zone="$SCW_ZONE" 2>&1); then
      # Detaching is asynchronous. Deleting the gateway while the GatewayNetwork
      # is still in `detaching` gets rejected, which is how you end up paying for
      # a gateway the teardown thought it had removed.
      wait_for_detach "$GATEWAY_NETWORK_ID" \
        || echo "WARNING: $GATEWAY_NETWORK_ID still detaching; the gateway delete may fail." >&2
    else
      echo "WARNING: failed to detach gateway network $GATEWAY_NETWORK_ID - check the console." >&2
      echo "$scw_out" >&2
    fi
  fi

  if [ -n "$GATEWAY_ID" ]; then
    # delete-ip=true matters: the default leaves the flexible IP reserved on the
    # project, so every run would strand one more billable IPv4.
    attempt=0
    while [ "$attempt" -lt 6 ]; do
      if scw_out=$(scw vpc-gw gateway delete "$GATEWAY_ID" delete-ip=true zone="$SCW_ZONE" 2>&1); then
        echo "Gateway $GATEWAY_ID and its flexible IP deleted." >&2
        GATEWAY_ID=""
        break
      fi
      attempt=$((attempt + 1))
      sleep 10
    done
    if [ -n "$GATEWAY_ID" ]; then
      echo "WARNING: could not delete gateway $GATEWAY_ID. It is billed until removed. Run:" >&2
      echo "  scw vpc-gw gateway delete $GATEWAY_ID delete-ip=true zone=$SCW_ZONE" >&2
      echo "$scw_out" >&2
    fi
  fi

  [ -n "$KNOWN_HOSTS_FILE" ] && rm -f "$KNOWN_HOSTS_FILE"
  [ -n "$KEY_FILE" ] && rm -f "$KEY_FILE"

  exit "$status"
}
# Explicit codes on signals: taking $? as-is can report 0 for a cancelled job,
# which would mark a killed migration as successful.
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# Never pipe `scw ... -o json` straight into `jq`: without pipefail (not
# POSIX, unavailable in dash) a failed `scw` call could still let a pipeline
# exit 0 if jq tolerates its empty/error input. Capturing to a variable first
# means `set -e` catches a failed `scw` call the normal way, before jq ever runs.
json_field() {
  # $1 = JSON text, $2 = jq filter
  printf '%s' "$1" | jq -r "$2"
}

require_field() {
  # $1 = JSON text, $2 = jq filter, $3 = human description.
  # jq -r prints the literal string "null" for a missing field and exits 0, so
  # without this a renamed API field propagates as a plausible-looking value.
  value=$(json_field "$1" "$2")
  if [ -z "$value" ] || [ "$value" = "null" ]; then
    echo "Could not read $3 (jq: $2) from the Scaleway response." >&2
    echo "If scw is old this may be an API v1/v2 mismatch - check 'scw version'." >&2
    return 1
  fi
  printf '%s' "$value"
}

wait_for_gateway_running() {
  gw_id="$1"
  attempt=0
  max_attempts=30 # ~5 minutes at 10s each
  gw_status="unknown"
  while [ "$attempt" -lt "$max_attempts" ]; do
    if gw_json=$(scw vpc-gw gateway get "$gw_id" zone="$SCW_ZONE" -o json 2>/dev/null); then
      gw_status=$(json_field "$gw_json" '.status')
      case "$gw_status" in
        running)
          printf '%s' "$gw_json"
          return 0
          ;;
        failed | locked | stopping | deleting)
          echo "Gateway $gw_id reached terminal state '$gw_status'." >&2
          return 1
          ;;
      esac
    fi
    attempt=$((attempt + 1))
    sleep 10
  done
  echo "Timed out waiting for gateway $gw_id to become running (last status: $gw_status)" >&2
  return 1
}

wait_for_gateway_network_ready() {
  gwnet_id="$1"
  attempt=0
  max_attempts=30 # ~2.5 minutes at 5s each
  gwnet_status="unknown"
  while [ "$attempt" -lt "$max_attempts" ]; do
    if gwnet_json=$(scw vpc-gw gateway-network get "$gwnet_id" zone="$SCW_ZONE" -o json 2>/dev/null); then
      gwnet_status=$(json_field "$gwnet_json" '.status')
      case "$gwnet_status" in
        ready) return 0 ;;
        detaching)
          echo "GatewayNetwork $gwnet_id is detaching instead of attaching." >&2
          return 1
          ;;
      esac
    fi
    attempt=$((attempt + 1))
    sleep 5
  done
  echo "Timed out waiting for GatewayNetwork $gwnet_id to be ready (last status: $gwnet_status)" >&2
  return 1
}

wait_for_detach() {
  # Gone from the API = fully detached.
  attempt=0
  while [ "$attempt" -lt 24 ]; do # ~2 minutes
    if ! scw vpc-gw gateway-network get "$1" zone="$SCW_ZONE" -o json >/dev/null 2>&1; then
      return 0
    fi
    attempt=$((attempt + 1))
    sleep 5
  done
  return 1
}

restrict_bastion_ips() {
  # A freshly enabled bastion has a single allowed range of 0.0.0.0/0. The scw
  # CLI does not currently expose the allowed-ips endpoints, so this goes
  # straight to the API. Verify the path against the Public Gateways API
  # reference if your scw/API version differs. Fails closed: if the allow-list
  # cannot be applied and confirmed, the run aborts and cleanup removes the gateway.
  : "${SCW_SECRET_KEY:?SCW_SECRET_KEY must be set to apply BASTION_ALLOWED_IPS}"

  ips_body=$(printf '%s' "$BASTION_ALLOWED_IPS" \
    | jq -R 'split(",") | map(gsub("^\\s+|\\s+$";"")) | map(select(length > 0)) | {ips: .}')

  echo "Restricting bastion access to: $BASTION_ALLOWED_IPS" >&2
  curl -fsS -X PUT \
    -H "X-Auth-Token: $SCW_SECRET_KEY" \
    -H "Content-Type: application/json" \
    -d "$ips_body" \
    "https://api.scaleway.com/vpc-gw/v2/zones/${SCW_ZONE}/gateways/${GATEWAY_ID}/bastion-allowed-ips" \
    >/dev/null

  check_json=$(scw vpc-gw gateway get "$GATEWAY_ID" zone="$SCW_ZONE" -o json)
  applied=$(json_field "$check_json" '.bastion_allowed_ips | sort | join(",")')
  expected=$(printf '%s' "$ips_body" | jq -r '.ips | sort | join(",")')
  if [ "$applied" != "$expected" ]; then
    echo "Bastion allow-list not applied as requested (got: $applied). Refusing to continue." >&2
    return 1
  fi
}

echo "Creating ephemeral bastion gateway in $SCW_ZONE..." >&2
gateway_name="ci-bastion-$(date +%s)-$$"
# enable-bastion is off by default - without it there is no sshd on the gateway
# and every run dies at the host-key fetch below.
create_json=$(scw vpc-gw gateway create \
  name="$gateway_name" \
  type="$GATEWAY_TYPE" \
  tags.0="$GATEWAY_TAG" \
  enable-bastion=true \
  bastion-port="$BASTION_PORT" \
  zone="$SCW_ZONE" \
  -o json)
GATEWAY_ID=$(require_field "$create_json" '.id' "the new gateway's ID")
echo "Gateway $GATEWAY_ID created, waiting for it to come up..." >&2

gw_json=$(wait_for_gateway_running "$GATEWAY_ID")

# API v2 calls this field `ipv4`; `ip` was the v1 name and yields a silent null.
GATEWAY_IP=$(require_field "$gw_json" '.ipv4.address' "the gateway's public IPv4 address")

if [ "$(json_field "$gw_json" '.bastion_enabled')" != "true" ]; then
  echo "Gateway $GATEWAY_ID came up without the SSH bastion enabled." >&2
  exit 1
fi

# Trust the API over our own default for the port actually in use.
reported_port=$(json_field "$gw_json" '.bastion_port')
case "$reported_port" in
  '' | null | 0) : ;;
  *) BASTION_PORT="$reported_port" ;;
esac
echo "Gateway running at $GATEWAY_IP, bastion on port $BASTION_PORT" >&2

if [ -n "$BASTION_ALLOWED_IPS" ]; then
  restrict_bastion_ips
else
  case "$(json_field "$gw_json" '.bastion_allowed_ips | join(",")')" in
    *0.0.0.0/0*)
      echo "WARNING: bastion is reachable from any public IP for this run." >&2
      echo "         Set BASTION_ALLOWED_IPS to the runner's egress IP to lock it down." >&2
      ;;
  esac
fi

echo "Attaching gateway to private network $SCW_PRIVATE_NETWORK_ID..." >&2
# No masquerade and no default route: we only need the gateway to reach the DB
# from its own interface on the private network. Enabling either would change
# routing for everything else already on this shared network.
gwnet_json=$(scw vpc-gw gateway-network create \
  gateway-id="$GATEWAY_ID" \
  private-network-id="$SCW_PRIVATE_NETWORK_ID" \
  zone="$SCW_ZONE" \
  -o json)
GATEWAY_NETWORK_ID=$(require_field "$gwnet_json" '.id' "the GatewayNetwork ID")
wait_for_gateway_network_ready "$GATEWAY_NETWORK_ID"

# Copy the key somewhere with known-good permissions: CI checkouts and secret
# mounts routinely land at 0644, which ssh refuses outright.
KEY_FILE=$(mktemp)
chmod 600 "$KEY_FILE"
cat "$BASTION_SSH_PRIVATE_KEY_FILE" > "$KEY_FILE"

KNOWN_HOSTS_FILE=$(mktemp)

# Note: this is trust-on-first-use. It pins the key for the rest of this run but
# does not authenticate the gateway, since we have no out-of-band fingerprint.
echo "Fetching the bastion's host key..." >&2
attempt=0
max_attempts=15
while [ "$attempt" -lt "$max_attempts" ]; do
  if ssh-keyscan -p "$BASTION_PORT" -T 5 "$GATEWAY_IP" > "$KNOWN_HOSTS_FILE" 2>/dev/null && [ -s "$KNOWN_HOSTS_FILE" ]; then
    break
  fi
  attempt=$((attempt + 1))
  sleep 5
done
if [ ! -s "$KNOWN_HOSTS_FILE" ]; then
  echo "Could not fetch the bastion's SSH host key from $GATEWAY_IP:$BASTION_PORT after $max_attempts attempts." >&2
  exit 1
fi

SSH_COMMON_OPTS="-o BatchMode=yes -o ConnectTimeout=10 -o IdentitiesOnly=yes"
SSH_COMMON_OPTS="$SSH_COMMON_OPTS -o UserKnownHostsFile=$KNOWN_HOSTS_FILE -o StrictHostKeyChecking=yes"

# End-to-end check before we commit to the tunnel. -W uses the same channel type
# as -L, so this proves auth, bastion reachability *and* that the DB endpoint is
# actually reachable from the gateway. ExitOnForwardFailure only covers the local
# bind, so a wrong DB_PRIVATE_IP would otherwise surface as a confusing failure
# inside <command> several minutes later.
echo "Checking $DB_PRIVATE_IP:$DB_PORT is reachable through the bastion..." >&2
# shellcheck disable=SC2086
if ! ssh $SSH_COMMON_OPTS \
  -i "$KEY_FILE" \
  -p "$BASTION_PORT" \
  -W "${DB_PRIVATE_IP}:${DB_PORT}" \
  "bastion@${GATEWAY_IP}" < /dev/null > /dev/null 2>&1; then
  echo "Could not open a connection to $DB_PRIVATE_IP:$DB_PORT via the bastion." >&2
  echo "Check DB_PRIVATE_IP/DB_PORT and that the DB is on $SCW_PRIVATE_NETWORK_ID." >&2
  exit 1
fi

echo "Opening tunnel: 127.0.0.1:$LOCAL_PORT -> $DB_PRIVATE_IP:$DB_PORT via bastion..." >&2
# shellcheck disable=SC2086
ssh -N $SSH_COMMON_OPTS \
  -o ExitOnForwardFailure=yes \
  -o ServerAliveInterval=30 \
  -o ServerAliveCountMax=3 \
  -i "$KEY_FILE" \
  -p "$BASTION_PORT" \
  -L "127.0.0.1:${LOCAL_PORT}:${DB_PRIVATE_IP}:${DB_PORT}" \
  "bastion@${GATEWAY_IP}" &
SSH_PID=$!

# -N backgrounded via "&" (not -f) so $! is the real, killable ssh process.
# ExitOnForwardFailure means a bad local bind or bad auth kills it almost
# immediately; the reachability probe above covers the far side.
sleep 2
if ! kill -0 "$SSH_PID" 2>/dev/null; then
  echo "SSH tunnel process died immediately - is 127.0.0.1:$LOCAL_PORT already in use?" >&2
  exit 1
fi

# Only the program name: arguments routinely carry connection strings with
# passwords, and this goes to the build log.
echo "Tunnel up, running: $1" >&2
set +e
"$@"
cmd_status=$?
set -e

exit "$cmd_status"

# Orphan sweeper (run on a schedule; SIGKILL and lost-ID races escape the trap):
#   scw vpc-gw gateway list tags.0=ci-ephemeral-bastion zone=all -o json \
#     | jq -r '.[] | select(.created_at < (now - 7200 | todate)) | .id'
# then delete each with delete-ip=true.

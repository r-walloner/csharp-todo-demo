#!/usr/bin/env bash
#
# Fills the Todo API with made-up example data so the UI has something to show.
#
# Re-running adds another copy of everything — it does not clear the database.
# Needs curl and GNU date (for the relative due dates).

set -euo pipefail

BASE="${BASE:-http://localhost:8080}"
API="$BASE/api/todo-lists"

# ---- helpers ----------------------------------------------------------------

# Quote a bash string as a JSON string.
json_str() {
  printf '"%s"' "$(printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g')"
}

# Pull the id out of a single-object response. The only "id": key in these
# payloads is the real one ("todoListId" does not match, the compare is
# case-sensitive), so this stays dependency-free — no jq, no python.
extract_id() {
  sed -n 's/.*"id":"\([^"]*\)".*/\1/p'
}

# A calendar day, N days from today, as midnight UTC. The columns are
# timestamptz and Npgsql rejects anything that is not Kind=Utc, so the Z matters.
day() {
  date -u -d "$1 days" +%Y-%m-%dT00:00:00.000Z
}

new_list() { # title description -> id
  curl -fsS -X POST "$API" \
    -H 'content-type: application/json' \
    -d "{\"title\":$(json_str "$1"),\"description\":$(json_str "$2")}" | extract_id
}

new_item() { # list_id title [priority] [dueAt] [notes] -> id
  local body="{\"title\":$(json_str "$2")"
  if [ -n "${3:-}" ]; then body="$body,\"priority\":$(json_str "$3")"; fi
  if [ -n "${4:-}" ]; then body="$body,\"dueAt\":$(json_str "$4")"; fi
  if [ -n "${5:-}" ]; then body="$body,\"notes\":$(json_str "$5")"; fi
  body="$body}"
  curl -fsS -X POST "$API/$1/items" \
    -H 'content-type: application/json' -d "$body" | extract_id
}

complete() { # list_id item_id
  curl -fsS -o /dev/null -X PUT "$API/$1/items/$2" \
    -H 'content-type: application/json' -d '{"isCompleted":true}'
}

# ---- preflight --------------------------------------------------------------

if ! curl -fsS -o /dev/null "$API" 2>/dev/null; then
  echo "Cannot reach $API" >&2
  echo "Is the API running, and has the database been migrated?" >&2
  exit 1
fi

echo "Seeding $BASE"

# ---- groceries --------------------------------------------------------------

echo "  Groceries"
groceries=$(new_list "Groceries" "Weekly shop at the market hall")
new_item "$groceries" "Oat milk, two cartons" "High" "$(day 2)" >/dev/null
new_item "$groceries" "Sourdough loaf" >/dev/null
new_item "$groceries" "Tomatoes and basil" "" "" "The good tomatoes are at the back stall" >/dev/null
new_item "$groceries" "Coffee beans" "VeryHigh" "$(day 1)" "Whole bean, not ground" >/dev/null
beans=$(new_item "$groceries" "Dish soap" "Low")
eggs=$(new_item "$groceries" "Eggs")
complete "$groceries" "$beans"
complete "$groceries" "$eggs"

# ---- work -------------------------------------------------------------------

echo "  Sprint 14"
work=$(new_list "Sprint 14" "Whatever did not fit into sprint 13")
new_item "$work" "Review the pagination PR" "VeryHigh" "$(day -1)" "Cursor pagination, replaces skip/take" >/dev/null
new_item "$work" "Write the migration notes" "High" "$(day 3)" >/dev/null
new_item "$work" "Add examples to the OpenAPI document" >/dev/null
new_item "$work" "Reply to the design review" "Medium" "$(day 0)" >/dev/null
new_item "$work" "Book a room for the retro" "Low" "$(day 5)" >/dev/null
standup=$(new_item "$work" "Move standup to 09:30")
complete "$work" "$standup"

# ---- flat -------------------------------------------------------------------

echo "  Around the flat"
home=$(new_list "Around the flat" "Small fixes that keep getting postponed")
new_item "$home" "Replace the hallway bulb" >/dev/null
new_item "$home" "Descale the kettle" "Low" >/dev/null
new_item "$home" "Hang the shelf in the study" "" "" "Brackets are in the drawer under the sink" >/dev/null
new_item "$home" "Bleed the radiators" "High" "$(day 7)" >/dev/null

# ---- reading ----------------------------------------------------------------

echo "  Reading"
reading=$(new_list "Reading" "Books and long articles, in no particular order")
new_item "$reading" "A Philosophy of Software Design" "High" >/dev/null
new_item "$reading" "The Postgres locking article Ana sent" "" "" "Bookmarked, about 40 minutes" >/dev/null
new_item "$reading" "Finish the Napoleon biography" "Low" >/dev/null
seeing=$(new_item "$reading" "Seeing Like a State")
crafting=$(new_item "$reading" "Crafting Interpreters, chapters 1 to 6" "Medium")
complete "$reading" "$seeing"
complete "$reading" "$crafting"

# ---- trip -------------------------------------------------------------------

echo "  Trip to Lisbon"
trip=$(new_list "Trip to Lisbon" "Early October, five nights")
new_item "$trip" "Renew passport" "VeryHigh" "$(day 14)" "Expires in November, appointment needed" >/dev/null
new_item "$trip" "Book flights" "High" "$(day 10)" >/dev/null
new_item "$trip" "Find a place to stay in Alfama" "Medium" "$(day 12)" >/dev/null
new_item "$trip" "Buy a phrasebook" "Low" >/dev/null
budget=$(new_item "$trip" "Agree the budget")
complete "$trip" "$budget"

# ---- one empty list, so the empty state is visible too ----------------------

echo "  Someday (left empty on purpose)"
new_list "Someday" "Ideas with no date attached yet" >/dev/null

# ---- summary ----------------------------------------------------------------

echo
curl -fsS "$API?pageSize=100" | sed -n 's/.*"totalCount":\([0-9]*\).*/Done. \1 lists in the database./p'
echo "Open $BASE/ to see them."

// AI-GENERATED: the whole UI under wwwroot/ was written by an AI coding
// assistant. Review before relying on it.

/**
 * DOM helpers and formatters.
 *
 * `el()` only ever assigns textContent and setAttribute — there is no
 * innerHTML anywhere in this codebase, which makes injection structurally
 * impossible rather than a discipline to remember.
 */

/**
 * @param {string} tag
 * @param {Object} [props]  class | text | dataset | on:{event:fn} | any attribute
 * @param {...(Node|string|number|null|false|Array)} children
 */
export function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);

  for (const [key, value] of Object.entries(props)) {
    if (value === null || value === undefined || value === false) continue;
    if (key === 'class') node.className = value;
    else if (key === 'text') node.textContent = value;
    else if (key === 'dataset') Object.assign(node.dataset, value);
    else if (key === 'on') {
      for (const [type, fn] of Object.entries(value)) node.addEventListener(type, fn);
    } else if (value === true) node.setAttribute(key, '');
    else node.setAttribute(key, String(value));
  }

  append(node, children);
  return node;
}

export function append(node, children) {
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return node;
}

export function clear(node) {
  while (node.firstChild) node.removeChild(node.firstChild);
  return node;
}

/** Screen-reader-only text node. */
export const sr = (text) => el('span', { class: 'sr-only', text });

/**
 * One listener per container. Rebuilt rows never need listeners reattached,
 * which is what makes the "rebuild the <ul>" render strategy safe.
 */
export function delegate(root, type, selector, handler) {
  root.addEventListener(type, (event) => {
    const match = event.target.closest(selector);
    if (match && root.contains(match)) handler(event, match);
  });
}

/* ---------- dates ----------
   dueAt is semantically a calendar day but the column is timestamptz, so it
   round-trips as midnight UTC. Every read formats with timeZone:'UTC' — mixing
   in one local-time formatter would reintroduce the classic "shows as the
   previous day west of Greenwich" bug. */

/** Defensively append Z to a timestamp that arrives without a zone designator. */
export function utcIso(iso) {
  if (!iso) return null;
  return /([Zz]|[+-]\d{2}:?\d{2})$/.test(iso) ? iso : `${iso}Z`;
}

export const parseUtc = (iso) => new Date(utcIso(iso));

/** The user's local calendar day, as the same YYYY-MM-DD shape <input type=date> emits. */
function localToday() {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

function addDay(isoDay) {
  const next = new Date(`${isoDay}T00:00:00Z`);
  next.setUTCDate(next.getUTCDate() + 1);
  return next.toISOString().slice(0, 10);
}

export function fmtDate(iso) {
  const date = parseUtc(iso);
  const sameYear = date.getUTCFullYear() === new Date().getFullYear();
  return new Intl.DateTimeFormat(undefined, {
    timeZone: 'UTC',
    day: 'numeric',
    month: 'short',
    year: sameYear ? undefined : 'numeric',
  }).format(date);
}

/**
 * @returns {{label: string, overdue: boolean}} Overdue is always spelled out —
 * never conveyed by colour alone.
 */
export function dueMeta(iso, isCompleted) {
  const day = utcIso(iso).slice(0, 10);
  const today = localToday();
  if (!isCompleted && day < today) return { label: 'Overdue', overdue: true };
  if (day === today) return { label: 'Today', overdue: false };
  if (day === addDay(today)) return { label: 'Tomorrow', overdue: false };
  return { label: fmtDate(iso), overdue: false };
}

export const PRIORITY_LABELS = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  VeryHigh: 'Very high',
};

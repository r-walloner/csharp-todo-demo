// AI-GENERATED: the whole UI under wwwroot/ was written by an AI coding
// assistant. Review before relying on it.

/**
 * The only module that knows about HTTP.
 *
 * Everything the API does that a caller shouldn't have to think about is
 * neutralised here: 204-with-no-body on every PUT/DELETE, PascalCase keys in
 * ValidationProblemDetails, exact enum casing, UTC serialisation of dueAt,
 * and the fields the update endpoints silently ignore.
 */

import { utcIso } from './dom.js';

const BASE = '/api/todo-lists';

/** Exact casing the API accepts. EnumDataType validates case-sensitively. */
export const PRIORITIES = ['Low', 'Medium', 'High', 'VeryHigh'];

export class ApiError extends Error {
  /** @param {{status?: number, kind?: string, fieldErrors?: Object|null, cause?: unknown}} [info] */
  constructor(message, { status = 0, kind = 'unknown', fieldErrors = null, cause } = {}) {
    super(message, { cause });
    this.name = 'ApiError';
    this.status = status;
    /** 'aborted' | 'offline' | 'network' | 'validation' | 'notfound' | 'server' | 'unknown' */
    this.kind = kind;
    this.fieldErrors = fieldErrors;
  }
}

export const isAbort = (error) => error instanceof ApiError && error.kind === 'aborted';

async function request(method, path, { body, signal } = {}) {
  let res;
  try {
    res = await fetch(path, {
      method,
      signal,
      headers: {
        Accept: 'application/json',
        ...(body ? { 'Content-Type': 'application/json' } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch (cause) {
    if (cause.name === 'AbortError') throw new ApiError('Aborted', { kind: 'aborted', cause });
    if (!navigator.onLine) throw new ApiError('You appear to be offline.', { kind: 'offline', cause });
    throw new ApiError("Couldn't reach the server. Is the app still running?", { kind: 'network', cause });
  }

  // Every PUT and DELETE answers 204. res.json() would throw on an empty body.
  if (res.status === 204 || res.headers.get('content-length') === '0') return null;

  // Development 500s render the HTML developer exception page, so gate parsing.
  const isJson = (res.headers.get('content-type') || '').includes('json');
  const payload = isJson ? await res.json().catch(() => null) : null;

  if (res.ok) return payload;

  if (res.status === 400) {
    // ValidationProblemDetails keys are NOT camelCased: JsonSerializerDefaults.Web
    // sets PropertyNamingPolicy but not DictionaryKeyPolicy, so they arrive as
    // "Title" (DataAnnotations) or "$.dueAt" (deserialization).
    const fieldErrors = {};
    for (const [key, value] of Object.entries(payload?.errors ?? {})) {
      fieldErrors[key.replace(/^\$\./, '').toLowerCase()] = Array.isArray(value) ? value : [String(value)];
    }
    const first = Object.values(fieldErrors)[0]?.[0];
    throw new ApiError(first || payload?.title || 'That input was rejected.', {
      status: 400,
      kind: 'validation',
      fieldErrors,
    });
  }

  // A malformed GUID fails the route constraint and 404s — never 400.
  if (res.status === 404) {
    throw new ApiError('That list or item no longer exists.', { status: 404, kind: 'notfound' });
  }

  throw new ApiError(payload?.detail || `Something went wrong on the server (${res.status}).`, {
    status: res.status,
    kind: 'server',
  });
}

/* ---------- normalisation ---------- */

function normalizeList(dto) {
  const itemCount = Math.max(0, dto.itemCount ?? 0);
  // Clamped because an out-of-range count would render a negative or >100% bar.
  const openItemCount = Math.min(itemCount, Math.max(0, dto.openItemCount ?? 0));
  return {
    ...dto,
    itemCount,
    openItemCount,
    completedCount: itemCount - openItemCount,
    createdAt: utcIso(dto.createdAt),
    updatedAt: utcIso(dto.updatedAt),
  };
}

function normalizeItem(dto) {
  return {
    ...dto,
    createdAt: utcIso(dto.createdAt),
    completedAt: utcIso(dto.completedAt),
    dueAt: utcIso(dto.dueAt),
  };
}

function normalizePage(page, mapItem) {
  return {
    items: (page?.items ?? []).map(mapItem),
    page: page?.page ?? 1,
    pageSize: page?.pageSize ?? 0,
    totalCount: page?.totalCount ?? 0,
    totalPages: page?.totalPages ?? 0,
  };
}

/* ---------- payload builders ----------
   Exported so the store can apply the *same* patch optimistically that the
   server will apply. That keeps the UI and the database in agreement,
   including about the keys deliberately dropped here. */

const text = (value) => (typeof value === 'string' ? value : '');

export function listPatch({ title, description }) {
  const body = {};
  // PUT has no [Required]; a blank title must never reach the wire.
  if (text(title).trim() !== '') body.title = title.trim();
  // "" is non-null, so the server writes it — description IS clearable.
  if (typeof description === 'string') body.description = description;
  return body;
}

export function itemPatch({ title, notes, priority, isCompleted, dueAt }) {
  const body = {};
  if (text(title).trim() !== '') body.title = title.trim();
  if (typeof notes === 'string') body.notes = notes;
  // "" passes EnumDataType validation and then silently resolves to Medium,
  // so only ever send one of the four exact-cased names.
  if (PRIORITIES.includes(priority)) body.priority = priority;
  if (typeof isCompleted === 'boolean') body.isCompleted = isCompleted;
  // The API cannot unset dueAt (`if (request.DueAt is not null)`), so an empty
  // value is omitted rather than pretending it clears. The UI offers no clear
  // affordance, and the stored value simply stays put.
  if (dueAt) body.dueAt = dueAt;
  return body;
}

/* ---------- dueAt codec ---------- */

export const toDueAtUtc = (day) => (day ? `${day}T00:00:00.000Z` : null);
export const fromDueAtUtc = (iso) => (iso ? utcIso(iso).slice(0, 10) : '');

/* ---------- endpoints ---------- */

const query = (params) =>
  Object.entries(params)
    .filter(([, value]) => value !== undefined && value !== null)
    .map(([key, value]) => `${key}=${encodeURIComponent(value)}`)
    .join('&');

export const api = {
  listLists: ({ page = 1, pageSize = 20, signal } = {}) =>
    request('GET', `${BASE}?${query({ page, pageSize })}`, { signal }).then((r) =>
      normalizePage(r, normalizeList),
    ),

  getList: (id, { signal } = {}) =>
    request('GET', `${BASE}/${id}`, { signal }).then(normalizeList),

  createList: ({ title, description }) =>
    request('POST', BASE, {
      body: {
        title: text(title).trim(),
        ...(text(description) ? { description } : {}),
      },
    }).then(normalizeList),

  /** @param body build it with listPatch() so the store can mirror it. */
  updateList: (id, body) => request('PUT', `${BASE}/${id}`, { body }),

  deleteList: (id) => request('DELETE', `${BASE}/${id}`),

  listItems: (listId, { isCompleted, page = 1, pageSize = 50, signal } = {}) =>
    request('GET', `${BASE}/${listId}/items?${query({ isCompleted, page, pageSize })}`, { signal }).then(
      (r) => normalizePage(r, normalizeItem),
    ),

  createItem: (listId, { title, notes, priority, dueAt }) =>
    request('POST', `${BASE}/${listId}/items`, {
      body: {
        title: text(title).trim(),
        ...(text(notes) ? { notes } : {}),
        ...(PRIORITIES.includes(priority) ? { priority } : {}),
        ...(dueAt ? { dueAt } : {}),
      },
    }).then(normalizeItem),

  /** @param body build it with itemPatch() so the store can mirror it. */
  updateItem: (listId, id, body) => request('PUT', `${BASE}/${listId}/items/${id}`, { body }),

  deleteItem: (listId, id) => request('DELETE', `${BASE}/${listId}/items/${id}`),
};

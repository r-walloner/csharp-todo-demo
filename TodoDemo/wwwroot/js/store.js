// AI-GENERATED: the whole UI under wwwroot/ was written by an AI coding
// assistant. Review before relying on it.

/**
 * Application state and every action that mutates it.
 *
 * Two conventions the rest of the app relies on:
 *
 *  1. Actions never throw. They catch, toast, and roll back, so no view needs
 *     a try/catch.
 *  2. Item and list objects are treated as immutable — they are replaced, never
 *     mutated in place. That is why a shallow array copy is a sufficient
 *     snapshot for rollback.
 *
 * PUT answers 204 with no body, so mutations are applied optimistically using
 * the very same patch object the request carries (built by api.itemPatch /
 * api.listPatch). The UI and the database therefore agree by construction,
 * including about the fields the API silently ignores.
 */

import { api, isAbort, itemPatch, listPatch } from './api.js';
import { toast } from './views/feedback.js';

const LIST_PAGE_SIZE = 20;
const ITEM_PAGE_SIZE = 50;

export const state = {
  route: { name: 'lists', listId: null },
  lists: {
    items: [],
    page: 0,
    pageSize: LIST_PAGE_SIZE,
    totalCount: 0,
    status: 'idle', // idle | loading | loadingMore | ready | error
    error: null,
  },
  detail: {
    listId: null,
    list: null,
    status: 'idle', // idle | loading | refreshing | ready | error
    error: null,
    errorKind: null,
    open: { items: [], page: 0, totalCount: 0, status: 'idle' },
    completed: { items: [], page: 0, totalCount: 0, status: 'idle' },
    filter: 'all', // all | open | done — pure view state, never a fetch
    editingItemId: null,
    editingList: false,
  },
  pending: new Set(),
};

/* ---------- subscriptions ---------- */

const subscribers = new Set();
let dirty = false;

export function subscribe(fn) {
  subscribers.add(fn);
  return () => subscribers.delete(fn);
}

/** Coalesces a burst of mutations into a single render. */
export function notify() {
  if (dirty) return;
  dirty = true;
  requestAnimationFrame(() => {
    dirty = false;
    for (const fn of subscribers) fn(state);
  });
}

const markPending = (id) => state.pending.add(id);
const clearPending = (id) => state.pending.delete(id);

/* ---------- shared helpers ---------- */

/**
 * Keeps the detail header and the cached overview card in step after a local
 * change, so navigating back shows the right counts without a refetch.
 */
function adjustCounts(listId, deltaItems, deltaOpen) {
  const patch = (list) => {
    const itemCount = Math.max(0, list.itemCount + deltaItems);
    const openItemCount = Math.min(itemCount, Math.max(0, list.openItemCount + deltaOpen));
    return { ...list, itemCount, openItemCount, completedCount: itemCount - openItemCount };
  };
  if (state.detail.list?.id === listId) state.detail.list = patch(state.detail.list);
  const index = state.lists.items.findIndex((list) => list.id === listId);
  if (index !== -1) state.lists.items[index] = patch(state.lists.items[index]);
}

/** Shallow snapshot of everything a single item mutation can touch. */
function snapshotDetail() {
  const { detail, lists } = state;
  const cachedIndex = lists.items.findIndex((list) => list.id === detail.listId);
  return {
    open: { items: detail.open.items.slice(), totalCount: detail.open.totalCount },
    completed: { items: detail.completed.items.slice(), totalCount: detail.completed.totalCount },
    list: detail.list,
    cachedIndex,
    cachedList: cachedIndex === -1 ? null : lists.items[cachedIndex],
  };
}

function restoreDetail(snapshot) {
  const { detail } = state;
  detail.open.items = snapshot.open.items;
  detail.open.totalCount = snapshot.open.totalCount;
  detail.completed.items = snapshot.completed.items;
  detail.completed.totalCount = snapshot.completed.totalCount;
  detail.list = snapshot.list;
  if (snapshot.cachedIndex !== -1) state.lists.items[snapshot.cachedIndex] = snapshot.cachedList;
}

const bucketFor = (item) => (item.isCompleted ? state.detail.completed : state.detail.open);

function dropItemLocally(item) {
  const bucket = bucketFor(item);
  const index = bucket.items.findIndex((candidate) => candidate.id === item.id);
  if (index === -1) return;
  bucket.items.splice(index, 1);
  bucket.totalCount = Math.max(0, bucket.totalCount - 1);
  adjustCounts(state.detail.listId, -1, item.isCompleted ? 0 : -1);
}

/** A 404 on a mutation means it is already gone — converge, never roll back. */
function handleMutationError(error, item, rollback) {
  if (isAbort(error)) return;
  if (error.kind === 'notfound' && item) {
    dropItemLocally(item);
    toast('That item no longer exists.', { kind: 'error' });
    return;
  }
  rollback();
  toast(error.message, { kind: 'error' });
}

/* ---------- overview ---------- */

let listsController = null;
let listsSeq = 0;

export async function loadLists() {
  listsController?.abort();
  const controller = (listsController = new AbortController());
  const seq = ++listsSeq;
  const { lists } = state;

  lists.status = lists.items.length ? 'ready' : 'loading';
  lists.error = null;
  notify();

  try {
    const page = await api.listLists({ page: 1, pageSize: LIST_PAGE_SIZE, signal: controller.signal });
    if (seq !== listsSeq) return;
    lists.items = page.items;
    lists.page = page.page;
    lists.pageSize = page.pageSize || LIST_PAGE_SIZE;
    lists.totalCount = page.totalCount;
    lists.status = 'ready';
  } catch (error) {
    if (isAbort(error) || seq !== listsSeq) return;
    lists.status = 'error';
    lists.error = error.message;
  }
  notify();
}

export async function loadMoreLists() {
  const { lists } = state;
  if (lists.status === 'loadingMore') return;
  lists.status = 'loadingMore';
  notify();

  try {
    const page = await api.listLists({ page: lists.page + 1, pageSize: lists.pageSize });
    // skip/take over CreatedAt DESC can repeat a row if something was created
    // since the previous page was fetched, so dedupe on the way in.
    const seen = new Set(lists.items.map((list) => list.id));
    lists.items = lists.items.concat(page.items.filter((list) => !seen.has(list.id)));
    lists.page = page.page;
    lists.totalCount = page.totalCount;
  } catch (error) {
    if (!isAbort(error)) toast(error.message, { kind: 'error' });
  }
  lists.status = 'ready';
  notify();
}

/* ---------- detail ---------- */

let detailController = null;
let detailSeq = 0;

export async function openDetail(listId) {
  detailController?.abort();
  const controller = (detailController = new AbortController());
  const seq = ++detailSeq;
  const { detail } = state;

  if (detail.listId !== listId) {
    detail.listId = listId;
    detail.list = null;
    detail.editingItemId = null;
    detail.editingList = false;
    detail.open = { items: [], page: 0, totalCount: 0, status: 'idle' };
    detail.completed = { items: [], page: 0, totalCount: 0, status: 'idle' };
  }
  detail.status = detail.list ? 'refreshing' : 'loading';
  detail.error = null;
  detail.errorKind = null;
  notify();

  try {
    // Three parallel requests. Splitting items by isCompleted makes the
    // completed count exact and turns the All/Open/Done filter into a pure
    // visibility toggle with no network traffic at all.
    const [list, open, completed] = await Promise.all([
      api.getList(listId, { signal: controller.signal }),
      api.listItems(listId, { isCompleted: false, pageSize: ITEM_PAGE_SIZE, signal: controller.signal }),
      api.listItems(listId, { isCompleted: true, pageSize: ITEM_PAGE_SIZE, signal: controller.signal }),
    ]);
    if (seq !== detailSeq) return;
    detail.list = list;
    detail.open = { items: open.items, page: open.page, totalCount: open.totalCount, status: 'ready' };
    detail.completed = {
      items: completed.items,
      page: completed.page,
      totalCount: completed.totalCount,
      status: 'ready',
    };
    detail.status = 'ready';
  } catch (error) {
    if (isAbort(error) || seq !== detailSeq) return;
    detail.status = 'error';
    detail.error = error.message;
    detail.errorKind = error.kind;
  }
  notify();
}

/** @param {'open'|'completed'} name */
export async function loadMoreItems(name) {
  const bucket = state.detail[name];
  const listId = state.detail.listId;
  if (bucket.status === 'loadingMore') return;
  bucket.status = 'loadingMore';
  notify();

  try {
    const page = await api.listItems(listId, {
      isCompleted: name === 'completed',
      page: bucket.page + 1,
      pageSize: ITEM_PAGE_SIZE,
    });
    const seen = new Set(bucket.items.map((item) => item.id));
    bucket.items = bucket.items.concat(page.items.filter((item) => !seen.has(item.id)));
    bucket.page = page.page;
    bucket.totalCount = page.totalCount;
  } catch (error) {
    if (!isAbort(error)) toast(error.message, { kind: 'error' });
  }
  bucket.status = 'ready';
  notify();
}

/* ---------- list mutations ---------- */

/** Creates are not optimistic: 201 returns the row with its server id. */
export async function createList(title) {
  const created = await api.createList({ title }).catch((error) => {
    if (!isAbort(error)) toast(error.message, { kind: 'error' });
    return null;
  });
  if (!created) return false;

  // CreatedAt DESC puts it first; the page window shifts by one, which the
  // dedupe in loadMoreLists absorbs.
  state.lists.items = [created, ...state.lists.items];
  state.lists.totalCount += 1;
  state.lists.status = 'ready';
  notify();
  return true;
}

export async function updateList(fields) {
  const { detail } = state;
  const list = detail.list;
  if (!list) return false;

  const body = listPatch(fields);
  if (Object.keys(body).length === 0) {
    detail.editingList = false;
    notify();
    return true;
  }

  const snapshot = snapshotDetail();
  const patched = { ...list, ...body };
  detail.list = patched;
  const cachedIndex = state.lists.items.findIndex((candidate) => candidate.id === list.id);
  if (cachedIndex !== -1) state.lists.items[cachedIndex] = patched;
  detail.editingList = false;
  markPending(list.id);
  notify();

  try {
    await api.updateList(list.id, body);
    return true;
  } catch (error) {
    if (error.kind === 'notfound') {
      toast('That list no longer exists.', { kind: 'error' });
      location.hash = '#/';
    } else if (!isAbort(error)) {
      restoreDetail(snapshot);
      detail.editingList = true;
      toast(error.message, { kind: 'error' });
    }
    return false;
  } finally {
    clearPending(list.id);
    notify();
  }
}

export async function deleteList(list) {
  const index = state.lists.items.findIndex((candidate) => candidate.id === list.id);
  const removed = index === -1 ? null : state.lists.items[index];
  if (index !== -1) {
    state.lists.items.splice(index, 1);
    state.lists.totalCount = Math.max(0, state.lists.totalCount - 1);
  }
  if (state.detail.listId === list.id) state.detail.listId = null;
  location.hash = '#/';
  notify();

  try {
    await api.deleteList(list.id);
  } catch (error) {
    if (error.kind === 'notfound') return; // already gone; the removal stands
    if (isAbort(error)) return;
    if (removed) state.lists.items.splice(index, 0, removed);
    state.lists.totalCount += 1;
    toast(error.message, { kind: 'error' });
    notify();
  }
}

/* ---------- item mutations ---------- */

export async function createItem(title) {
  const listId = state.detail.listId;
  const created = await api.createItem(listId, { title }).catch((error) => {
    if (!isAbort(error)) toast(error.message, { kind: 'error' });
    return null;
  });
  if (!created) return false;

  state.detail.open.items = [created, ...state.detail.open.items];
  state.detail.open.totalCount += 1;
  adjustCounts(listId, 1, 1);
  notify();
  return true;
}

export async function updateItem(item, fields) {
  const { detail } = state;
  const body = itemPatch(fields);

  detail.editingItemId = null;
  if (Object.keys(body).length === 0) {
    notify();
    return true;
  }

  const bucket = bucketFor(item);
  const index = bucket.items.findIndex((candidate) => candidate.id === item.id);
  if (index === -1) {
    notify();
    return false;
  }

  const snapshot = snapshotDetail();
  // Applying exactly `body` is what makes an emptied due date snap back:
  // itemPatch omitted the key, so the stored value is left untouched here too.
  bucket.items[index] = { ...bucket.items[index], ...body };
  markPending(item.id);
  notify();

  try {
    await api.updateItem(detail.listId, item.id, body);
    return true;
  } catch (error) {
    handleMutationError(error, item, () => restoreDetail(snapshot));
    return false;
  } finally {
    clearPending(item.id);
    notify();
  }
}

/**
 * The highest-frequency action, and the one that moves an item between the two
 * buckets: out of open, onto the front of completed, with both totals and the
 * parent list's counts adjusted.
 */
export async function setItemCompleted(item, isCompleted) {
  const { detail } = state;
  // The checkbox stays enabled so keyboard focus survives the re-render, so
  // ignore a second toggle while one is in flight and re-render to put the
  // checkbox back in sync with state.
  if (state.pending.has(item.id)) {
    notify();
    return;
  }
  const from = isCompleted ? detail.open : detail.completed;
  const to = isCompleted ? detail.completed : detail.open;
  const index = from.items.findIndex((candidate) => candidate.id === item.id);
  if (index === -1) return;

  const snapshot = snapshotDetail();
  const moved = {
    ...from.items[index],
    isCompleted,
    completedAt: isCompleted ? new Date().toISOString() : null,
  };

  from.items.splice(index, 1);
  from.totalCount = Math.max(0, from.totalCount - 1);
  to.items = [moved, ...to.items];
  to.totalCount += 1;
  adjustCounts(detail.listId, 0, isCompleted ? -1 : 1);
  markPending(item.id);
  notify();

  try {
    await api.updateItem(detail.listId, item.id, itemPatch({ isCompleted }));
  } catch (error) {
    handleMutationError(error, moved, () => restoreDetail(snapshot));
  } finally {
    clearPending(item.id);
    notify();
  }
}

export async function deleteItem(item) {
  const { detail } = state;
  const snapshot = snapshotDetail();

  detail.editingItemId = null;
  dropItemLocally(item);
  markPending(item.id);
  notify();

  try {
    await api.deleteItem(detail.listId, item.id);
  } catch (error) {
    if (error.kind === 'notfound' || isAbort(error)) return; // already gone
    restoreDetail(snapshot);
    toast(error.message, { kind: 'error' });
  } finally {
    clearPending(item.id);
    notify();
  }
}

/* ---------- view state ---------- */

export function setFilter(filter) {
  state.detail.filter = filter;
  notify();
}

export function setEditingItem(id) {
  state.detail.editingItemId = id;
  notify();
}

export function setEditingList(flag) {
  state.detail.editingList = flag;
  notify();
}

export function findItem(id) {
  return (
    state.detail.open.items.find((item) => item.id === id) ??
    state.detail.completed.items.find((item) => item.id === id) ??
    null
  );
}

export function retryCurrentView() {
  if (state.route.name === 'detail' && state.route.listId) openDetail(state.route.listId);
  else loadLists();
}

// AI-GENERATED: the whole UI under wwwroot/ was written by an AI coding
// assistant. Review before relying on it.

/**
 * List detail: item rows, the inline row editor, the list editor, the
 * All/Open/Done filter and the add-item composer.
 *
 * Render strategy — persistent shell, rebuilt lists. The two <ul>s are cleared
 * and rebuilt on every render; everything that holds state (both editors, the
 * composer) is created once in index.html and only ever moved or updated.
 * Rebuilding an editor would destroy the caret mid-keystroke.
 */

import { PRIORITIES, fromDueAtUtc, toDueAtUtc } from '../api.js';
import { PRIORITY_LABELS, append, clear, delegate, dueMeta, el, sr } from '../dom.js';
import {
  createItem,
  deleteItem,
  deleteList,
  findItem,
  loadMoreItems,
  retryCurrentView,
  setEditingItem,
  setEditingList,
  setFilter,
  setItemCompleted,
  state,
  updateItem,
  updateList,
} from '../store.js';
import { confirmDialog } from './feedback.js';

let view;
let head;
let headDesc;
let filterSet;
let openSection;
let openLabel;
let openRows;
let openFoot;
let completedBox;
let completedLabel;
let doneRows;
let doneFoot;
let statusFoot;

let editorHome;
let itemForm;
let itemTitle;
let itemNotes;
let itemPriority;
let itemDue;

let listForm;
let listTitle;
let listDesc;

let composer;
let composerInput;
let composerSubmit;

/* Transition trackers: fields are populated only when the editor opens, never
   on every render, or typing would be overwritten. */
let lastEditingItemId = null;
let lastEditingList = false;
let returnFocusItemId = null;
let returnFocusListEdit = false;
let focusAfterMore = null; // {bucket: 'open'|'completed', index: number}

export function mount() {
  view = document.getElementById('view-detail');
  head = document.getElementById('detail-head');
  headDesc = document.getElementById('detail-desc');
  filterSet = document.getElementById('detail-filter');
  openSection = document.getElementById('detail-open-section');
  openLabel = document.getElementById('detail-open-label');
  openRows = document.getElementById('detail-open-rows');
  openFoot = document.getElementById('detail-open-foot');
  completedBox = document.getElementById('detail-completed');
  completedLabel = document.getElementById('detail-completed-label');
  doneRows = document.getElementById('detail-completed-rows');
  doneFoot = document.getElementById('detail-completed-foot');
  statusFoot = document.getElementById('detail-status');

  editorHome = document.getElementById('editor-home');
  itemForm = document.getElementById('form-edit-item');
  itemTitle = document.getElementById('edit-item-title');
  itemNotes = document.getElementById('edit-item-notes');
  itemPriority = document.getElementById('edit-item-priority');
  itemDue = document.getElementById('edit-item-due');

  listForm = document.getElementById('form-edit-list');
  listTitle = document.getElementById('edit-list-title');
  listDesc = document.getElementById('edit-list-desc');

  composer = document.getElementById('form-new-item');
  composerInput = document.getElementById('new-item-title');
  composerSubmit = composer.querySelector('button[type="submit"]');

  // One click listener for the whole view. Rebuilt rows never need listeners
  // reattached, which is what makes the rebuild strategy cheap and safe.
  delegate(view, 'click', '[data-action]', onAction);
  delegate(view, 'change', 'input[type="checkbox"][data-item-id]', onToggle);

  filterSet.addEventListener('change', (event) => setFilter(event.target.value));
  itemForm.addEventListener('submit', onSaveItem);
  listForm.addEventListener('submit', onSaveList);
  composer.addEventListener('submit', onCreateItem);

  for (const form of [itemForm, listForm]) {
    form.addEventListener('keydown', (event) => {
      if (event.key !== 'Escape') return;
      event.preventDefault();
      if (form === itemForm) cancelItemEdit();
      else cancelListEdit();
    });
  }
}

/* ---------- actions ---------- */

function onAction(event, target) {
  const { action, itemId } = target.dataset;
  switch (action) {
    case 'open-editor':
      setEditingItem(itemId);
      break;
    case 'cancel-item-edit':
      cancelItemEdit();
      break;
    case 'delete-item':
      onDeleteItem();
      break;
    case 'edit-list':
      setEditingList(true);
      break;
    case 'cancel-list-edit':
      cancelListEdit();
      break;
    case 'delete-list':
      onDeleteList();
      break;
    case 'load-more-open':
      focusAfterMore = { bucket: 'open', index: state.detail.open.items.length };
      loadMoreItems('open');
      break;
    case 'load-more-completed':
      focusAfterMore = { bucket: 'completed', index: state.detail.completed.items.length };
      loadMoreItems('completed');
      break;
    case 'retry':
      retryCurrentView();
      break;
    default:
      break;
  }
}

function onToggle(event, checkbox) {
  const item = findItem(checkbox.dataset.itemId);
  if (item) setItemCompleted(item, checkbox.checked);
}

function cancelItemEdit() {
  returnFocusItemId = state.detail.editingItemId;
  setEditingItem(null);
}

function cancelListEdit() {
  returnFocusListEdit = true;
  setEditingList(false);
}

async function onSaveItem(event) {
  event.preventDefault();
  const item = findItem(state.detail.editingItemId);
  if (!item) return;

  // TodoItemsController's PUT would happily store a whitespace title, so this
  // check is the guard rather than a nicety.
  const title = itemTitle.value.trim();
  if (title === '') {
    itemTitle.focus();
    return;
  }

  returnFocusItemId = item.id;
  await updateItem(item, {
    title,
    notes: itemNotes.value,
    priority: itemPriority.value,
    dueAt: toDueAtUtc(itemDue.value),
  });
}

async function onDeleteItem() {
  const item = findItem(state.detail.editingItemId);
  if (!item) return;

  const confirmed = await confirmDialog({
    title: 'Delete this item?',
    body: `"${item.title}" will be removed.`,
  });
  if (!confirmed) {
    itemTitle.focus();
    return;
  }
  returnFocusItemId = null;
  deleteItem(item);
}

async function onSaveList(event) {
  event.preventDefault();
  const title = listTitle.value.trim();
  if (title === '') {
    listTitle.focus();
    return;
  }
  returnFocusListEdit = true;
  await updateList({ title, description: listDesc.value });
}

async function onDeleteList() {
  const list = state.detail.list;
  if (!list) return;

  const confirmed = await confirmDialog({
    title: `Delete "${list.title}"?`,
    // The cascade is invisible otherwise.
    body:
      list.itemCount === 0
        ? 'This list has no items.'
        : `This also deletes its ${list.itemCount} item${list.itemCount === 1 ? '' : 's'}.`,
    confirmLabel: 'Delete list',
  });
  if (confirmed) deleteList(list);
}

async function onCreateItem(event) {
  event.preventDefault();
  const title = composerInput.value.trim();
  if (title === '') return;

  composerSubmit.disabled = true;
  composer.setAttribute('aria-busy', 'true');
  const created = await createItem(title);
  composerSubmit.disabled = false;
  composer.removeAttribute('aria-busy');

  if (created) composerInput.value = '';
  composerInput.focus();
}

/* ---------- row rendering ---------- */

function itemRow(item) {
  const pending = state.pending.has(item.id);

  const meta = [];
  // Medium is the default; chipping it would make every row noisy.
  if (item.priority && item.priority !== 'Medium') {
    meta.push(
      el(
        'span',
        { class: `chip p-${item.priority.toLowerCase()}` },
        el('span', { class: 'dot', 'aria-hidden': 'true' }),
        PRIORITY_LABELS[item.priority] ?? item.priority,
      ),
    );
  }
  if (item.dueAt) {
    const { label, overdue } = dueMeta(item.dueAt, item.isCompleted);
    meta.push(el('span', { class: overdue ? 'chip overdue' : 'chip', text: label }));
  }

  // Deliberately not disabled while pending: disabling steals keyboard focus
  // mid-toggle. The store ignores a second toggle instead.
  const checkbox = el('input', {
    type: 'checkbox',
    class: 'check',
    checked: item.isCompleted,
    dataset: { itemId: item.id },
  });

  const toggle = el(
    'label',
    { class: 'check-wrap' },
    checkbox,
    sr(`Mark "${item.title}" as ${item.isCompleted ? 'not complete' : 'complete'}`),
  );

  // A target of its own, so tapping the title never toggles completion.
  const main = el(
    'button',
    { type: 'button', class: 'row-main', dataset: { action: 'open-editor', itemId: item.id } },
    el('span', { class: 'row-title', text: item.title }),
    el('span', { class: 'chev', 'aria-hidden': 'true', text: '›' }),
    meta.length ? el('span', { class: 'row-meta' }, meta) : null,
    item.notes ? el('span', { class: 'row-notes', text: item.notes }) : null,
  );

  const row = el(
    'div',
    { class: item.isCompleted ? 'row row-done' : 'row', 'data-pending': pending ? '' : null },
    toggle,
    main,
  );

  return el('li', { class: 'row-wrap', dataset: { itemId: item.id } }, row);
}

function loadMoreButton(bucket, action) {
  const remaining = Math.max(0, bucket.totalCount - bucket.items.length);
  if (remaining === 0) return null;
  const busy = bucket.status === 'loadingMore';
  return el('button', {
    type: 'button',
    class: 'btn loadmore',
    dataset: { action },
    disabled: busy,
    'aria-busy': busy ? 'true' : null,
    text: busy ? 'Loading…' : `Load more (${remaining} remaining)`,
  });
}

const skeletons = (count) =>
  el(
    'div',
    { class: 'skeletons' },
    Array.from({ length: count }, () => el('div', { class: 'skeleton' })),
  );

const emptyState = (title, help) =>
  el(
    'div',
    { class: 'empty' },
    el('p', { class: 'empty-title', text: title }),
    help ? el('p', { class: 'empty-help', text: help }) : null,
  );

function errorPanel(detail) {
  const gone = detail.errorKind === 'notfound';
  return el(
    'div',
    { class: 'panel' },
    el('p', { class: 'panel-msg', text: gone ? 'This list no longer exists.' : detail.error }),
    gone
      ? el('a', { class: 'btn btn-ghost', href: '#/', text: 'Back to all lists' })
      : el('button', { type: 'button', class: 'btn btn-ghost', dataset: { action: 'retry' }, text: 'Retry' }),
  );
}

/* ---------- focus preservation ----------
   Rebuilding the rows destroys whatever node currently has focus, and a render
   can be triggered by anything: a pending flag clearing, another row settling, a
   background refresh. So focus is described before the rebuild and re-resolved
   afterwards — by node for the editors (to keep the caret) and by selector for
   rows, since a toggled item is re-created in the *other* section. */

function describeFocus() {
  const active = document.activeElement;
  if (!active || !view.contains(active)) return null;

  if (itemForm.contains(active) || listForm.contains(active)) {
    const hasCaret =
      active instanceof HTMLTextAreaElement || (active instanceof HTMLInputElement && active.type === 'text');
    return {
      node: active,
      start: hasCaret ? active.selectionStart : null,
      end: hasCaret ? active.selectionEnd : null,
    };
  }

  const { action, itemId } = active.dataset ?? {};
  const id = itemId ? `[data-item-id="${CSS.escape(itemId)}"]` : '';
  if (action) return { selector: `[data-action="${action}"]${id}` };
  if (active instanceof HTMLInputElement && active.type === 'checkbox' && itemId) {
    return { selector: `input[type="checkbox"]${id}` };
  }
  return null;
}

function restoreDescribedFocus(snapshot) {
  if (!snapshot) return;
  if (snapshot.node) {
    if (!snapshot.node.isConnected) return;
    snapshot.node.focus();
    if (snapshot.start !== null) snapshot.node.setSelectionRange(snapshot.start, snapshot.end);
    return;
  }
  view.querySelector(snapshot.selector)?.focus();
}

/* ---------- render ---------- */

export function render() {
  const detail = state.detail;
  const { open, completed, filter } = detail;
  const focus = describeFocus();

  // Park the row editor before the <ul>s are cleared, or it would be destroyed
  // along with the row it is currently sitting in.
  editorHome.append(itemForm);

  clear(openRows);
  clear(doneRows);
  clear(openFoot);
  clear(doneFoot);
  clear(statusFoot);

  const failed = detail.status === 'error';
  const loading = detail.status === 'loading';
  const ready = !failed && !loading && detail.list !== null;

  head.hidden = !ready || detail.editingList;
  listForm.hidden = !ready || !detail.editingList;
  filterSet.hidden = !ready;
  // Symmetric: a section appears only when the filter allows it and it has rows.
  // The zero cases are covered by the empty states below instead of an empty box.
  openSection.hidden = !ready || filter === 'done' || open.totalCount === 0;
  completedBox.hidden = !ready || filter === 'open' || completed.totalCount === 0;

  if (loading) {
    statusFoot.append(skeletons(3));
    return;
  }
  if (failed) {
    statusFoot.append(errorPanel(detail));
    return;
  }
  if (!ready) return;

  headDesc.textContent = detail.list.description ?? '';
  headDesc.hidden = !detail.list.description;

  // Filter is a pure visibility toggle over already-loaded state — no fetch.
  for (const radio of filterSet.querySelectorAll('input[name="filter"]')) {
    radio.checked = radio.value === filter;
  }

  for (const item of open.items) openRows.append(itemRow(item));
  for (const item of completed.items) doneRows.append(itemRow(item));

  // Exact counts, because each comes from its own filtered query's totalCount
  // rather than from however many rows happen to be on the current page.
  openLabel.textContent = `Open (${open.totalCount})`;
  completedLabel.textContent = `Completed (${completed.totalCount})`;

  // append() filters nulls; Node.append(null) would insert the text "null".
  append(openFoot, [loadMoreButton(open, 'load-more-open')]);
  append(doneFoot, [loadMoreButton(completed, 'load-more-completed')]);

  if (open.totalCount === 0 && completed.totalCount === 0) {
    statusFoot.append(emptyState('Nothing here yet', 'Add an item below.'));
  } else if (filter === 'open' && open.totalCount === 0) {
    statusFoot.append(emptyState('No open items', 'Everything here is done.'));
  } else if (filter === 'done' && completed.totalCount === 0) {
    statusFoot.append(emptyState('No completed items'));
  }

  const reopened = placeItemEditor();
  const listEditorOpened = syncListEditor();
  // A freshly opened editor focuses its own first field. Otherwise: an explicit
  // hand-off wins, and failing that focus goes back where it was.
  if (!reopened && !listEditorOpened && !handOffFocus()) restoreDescribedFocus(focus);
}

/** @returns {boolean} true when the editor just opened on a different row. */
function placeItemEditor() {
  const id = state.detail.editingItemId;
  const item = id ? findItem(id) : null;
  const row = item ? view.querySelector(`li[data-item-id="${CSS.escape(id)}"]`) : null;

  if (!row) {
    itemForm.hidden = true;
    lastEditingItemId = null;
    return false;
  }

  row.append(itemForm);
  itemForm.hidden = false;

  if (id === lastEditingItemId) return false;

  itemTitle.value = item.title;
  itemNotes.value = item.notes ?? '';
  itemPriority.value = PRIORITIES.includes(item.priority) ? item.priority : 'Medium';
  itemDue.value = fromDueAtUtc(item.dueAt);
  lastEditingItemId = id;
  itemTitle.focus();
  return true;
}

/** @returns {boolean} true when the list editor just opened. */
function syncListEditor() {
  const { editingList, list } = state.detail;
  const opened = editingList && !lastEditingList && list !== null;
  lastEditingList = editingList;
  if (!opened) return false;
  listTitle.value = list.title;
  listDesc.value = list.description ?? '';
  listTitle.focus();
  return true;
}

/**
 * Deliberate focus moves after an action: back to the row whose editor just
 * closed, back to Edit after the list editor closed, or onto the first row a
 * "Load more" produced (its button no longer exists to hold focus).
 *
 * @returns {boolean} true when focus was placed.
 */
function handOffFocus() {
  if (returnFocusItemId) {
    const id = returnFocusItemId;
    returnFocusItemId = null;
    const button = view.querySelector(`[data-action="open-editor"][data-item-id="${CSS.escape(id)}"]`);
    if (button) {
      button.focus();
      return true;
    }
  }

  if (returnFocusListEdit && !state.detail.editingList) {
    returnFocusListEdit = false;
    const button = view.querySelector('[data-action="edit-list"]');
    if (button) {
      button.focus();
      return true;
    }
  }

  if (focusAfterMore) {
    const { bucket, index } = focusAfterMore;
    if (state.detail[bucket].items.length > index) {
      focusAfterMore = null;
      const rows = bucket === 'open' ? openRows : doneRows;
      const button = rows.children[index]?.querySelector('[data-action="open-editor"]');
      if (button) {
        button.focus();
        return true;
      }
    }
  }

  return false;
}

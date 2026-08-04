// AI-GENERATED: the whole UI under wwwroot/ was written by an AI coding
// assistant. Review before relying on it.

/** Overview: every list as a card, plus the new-list composer. */

import { clear, delegate, el } from '../dom.js';
import { createList, loadLists, loadMoreLists, state } from '../store.js';

let rows;
let foot;
let form;
let input;
let submit;

/** Index of the first row added by the most recent "Load more", pending focus. */
let focusAfterMore = null;

export function mount() {
  rows = document.getElementById('lists-rows');
  foot = document.getElementById('lists-foot');
  form = document.getElementById('form-new-list');
  input = document.getElementById('new-list-title');
  submit = form.querySelector('button[type="submit"]');

  form.addEventListener('submit', onCreate);

  delegate(foot, 'click', '[data-action]', (event, target) => {
    if (target.dataset.action === 'load-more') {
      focusAfterMore = state.lists.items.length;
      loadMoreLists();
    } else if (target.dataset.action === 'retry') {
      loadLists();
    }
  });
}

async function onCreate(event) {
  event.preventDefault();
  const title = input.value.trim();
  if (!title) return;

  submit.disabled = true;
  form.setAttribute('aria-busy', 'true');
  const created = await createList(title);
  submit.disabled = false;
  form.removeAttribute('aria-busy');

  // Clear on success only — a failed create must not lose what was typed.
  if (created) input.value = '';
  // Keep focus so several lists can be added without leaving the field.
  input.focus();
}

/* ---------- rendering ---------- */

function card(list) {
  const done = list.completedCount;
  const percent = list.itemCount === 0 ? 0 : Math.round((done / list.itemCount) * 100);

  const bar = el('div', { class: 'bar', 'aria-hidden': 'true' }, el('div', { class: 'bar-fill' }));
  bar.style.setProperty('--pct', `${percent}%`);

  const foot =
    list.itemCount === 0
      ? el('p', { class: 'card-foot', text: 'No items yet' })
      : el(
          'div',
          { class: 'card-foot' },
          bar,
          // The bar is aria-hidden because this text already says it.
          el('span', { class: 'card-count', text: `${done}/${list.itemCount} done` }),
        );

  // The whole card is one anchor: native tap target, focus ring and keyboard
  // Enter for free. That is also why it holds no other interactive element.
  const link = el(
    'a',
    { class: 'card', href: `#/lists/${list.id}` },
    el('span', { class: 'card-title', text: list.title }),
    el('span', { class: 'chev', 'aria-hidden': 'true', text: '›' }),
    list.description ? el('p', { class: 'card-desc', text: list.description }) : null,
    foot,
  );

  return el('li', { class: 'card-wrap' }, link);
}

const skeletons = (count) =>
  el(
    'div',
    { class: 'skeletons' },
    Array.from({ length: count }, () => el('div', { class: 'skeleton' })),
  );

const emptyState = () =>
  el(
    'div',
    { class: 'empty' },
    el('p', { class: 'empty-title', text: 'No lists yet' }),
    // Points at the composer rather than autofocusing it: on mobile an
    // unbidden keyboard shoves the content off screen.
    el('p', { class: 'empty-help', text: 'Create your first one below.' }),
  );

const errorPanel = (message) =>
  el(
    'div',
    { class: 'panel' },
    el('p', { class: 'panel-msg', text: message }),
    el('button', { type: 'button', class: 'btn btn-ghost', 'data-action': 'retry', text: 'Retry' }),
  );

export function render() {
  const lists = state.lists;

  clear(rows);
  for (const list of lists.items) rows.append(card(list));

  clear(foot);
  if (lists.status === 'loading') {
    foot.append(skeletons(3));
  } else if (lists.status === 'error') {
    foot.append(errorPanel(lists.error));
  } else if (lists.items.length === 0) {
    foot.append(emptyState());
  } else {
    const remaining = Math.max(0, lists.totalCount - lists.items.length);
    if (remaining > 0) {
      foot.append(
        el('button', {
          type: 'button',
          class: 'btn loadmore',
          'data-action': 'load-more',
          disabled: lists.status === 'loadingMore',
          'aria-busy': lists.status === 'loadingMore' ? 'true' : null,
          text: lists.status === 'loadingMore' ? 'Loading…' : `Load more (${remaining} remaining)`,
        }),
      );
    } else if (lists.page > 1) {
      foot.append(el('p', { class: 'endnote', text: "That's everything." }));
    }
  }

  // The "Load more" button is rebuilt, so focus would be dropped on the floor.
  // Move it to the first newly-loaded card instead.
  if (focusAfterMore !== null && lists.items.length > focusAfterMore) {
    rows.children[focusAfterMore]?.querySelector('a')?.focus();
    focusAfterMore = null;
  }
}

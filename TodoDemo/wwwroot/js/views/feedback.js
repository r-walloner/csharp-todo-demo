// AI-GENERATED: the whole UI under wwwroot/ was written by an AI coding
// assistant. Review before relying on it.

/** Toasts and the shared confirm dialog. */

import { el } from '../dom.js';

const SUCCESS_MS = 4000;

let toasts;
let dialog;
let dialogTitle;
let dialogBody;
let dialogOk;

export function mount() {
  toasts = document.getElementById('toasts');
  dialog = document.getElementById('confirm');
  dialogTitle = document.getElementById('confirm-title');
  dialogBody = document.getElementById('confirm-body');
  dialogOk = document.getElementById('confirm-ok');
}

/**
 * Errors persist until dismissed; successes fade after 4s.
 * Children are appended and removed individually — rebuilding the aria-live
 * container would swallow announcements.
 *
 * @param {string} message
 * @param {{kind?: 'info'|'error'}} [options]
 */
export function toast(message, { kind = 'info' } = {}) {
  if (!toasts) return;
  const isError = kind === 'error';

  const node = el(
    'div',
    { class: isError ? 'toast toast-error' : 'toast', role: isError ? 'alert' : undefined },
    el('span', { class: 'toast-msg', text: message }),
  );

  const close = el('button', {
    type: 'button',
    class: 'toast-close',
    'aria-label': 'Dismiss',
    on: { click: () => node.remove() },
  });
  close.append(el('span', { 'aria-hidden': 'true', text: '×' }));
  node.append(close);

  toasts.append(node);
  if (!isError) setTimeout(() => node.remove(), SUCCESS_MS);
}

/**
 * Increments on every open. The `close` event is delivered in a queued task, not
 * synchronously, so a dialog that is dismissed and immediately reopened can
 * deliver the first close *after* the second open has already reset
 * returnValue. Without this token the stale listener reads the new answer and
 * the caller acts on a confirmation the user never gave for it — which showed up
 * as one confirmed delete issuing two DELETE requests.
 */
let openToken = 0;

/**
 * Native <dialog> gives a real focus trap, Esc-to-dismiss, ::backdrop and an
 * inert background for free — the one place a modal is cheaper than not.
 *
 * @param {{title: string, body?: string, confirmLabel?: string}} options
 * @returns {Promise<boolean>}
 */
export function confirmDialog({ title, body = '', confirmLabel = 'Delete' }) {
  if (!dialog) return Promise.resolve(false);

  dialogTitle.textContent = title;
  dialogBody.textContent = body;
  dialogBody.hidden = body === '';
  dialogOk.textContent = confirmLabel;

  const opener = document.activeElement;
  const token = ++openToken;

  return new Promise((resolve) => {
    dialog.addEventListener(
      'close',
      () => {
        // Superseded by a later open: this call was dismissed, whatever the
        // dialog now says.
        if (token !== openToken) {
          resolve(false);
          return;
        }
        // Esc closes with an empty returnValue, which reads as cancel.
        resolve(dialog.returnValue === 'confirm');
        if (opener instanceof HTMLElement && opener.isConnected) opener.focus();
      },
      { once: true },
    );
    dialog.returnValue = '';
    if (dialog.open) dialog.close('');
    dialog.showModal();
  });
}

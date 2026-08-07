// AI-GENERATED: the whole UI under wwwroot/ was written by an AI coding
// assistant. Review before relying on it.

/**
 * Bootstrap, hash router and the top-level render switch.
 *
 * Hash routing keeps the server config at zero: it only ever serves /,
 * /css/app.css and /js/*. No fallback route means a typo'd API path still
 * answers 404 instead of quietly returning this page's HTML.
 */

import { loadLists, notify, openDetail, retryCurrentView, state, subscribe } from './store.js';
import * as detail from './views/detail.js';
import * as feedback from './views/feedback.js';
import * as lists from './views/lists.js';

const LIST_ROUTE = /^#\/lists\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i;

const viewLists = document.getElementById('view-lists');
const viewDetail = document.getElementById('view-detail');
const backLink = document.getElementById('back-link');
const appMark = document.getElementById('app-mark');
const appTitle = document.getElementById('app-title');
const formNewList = document.getElementById('form-new-list');
const formNewItem = document.getElementById('form-new-item');

let focusHeadingNext = false;

function parseHash() {
  const match = LIST_ROUTE.exec(location.hash);
  return match ? { name: 'detail', listId: match[1] } : { name: 'lists', listId: null };
}

function handleRoute() {
  const route = parseHash();

  // Anything unrecognised — including a malformed GUID — falls back to the
  // overview. Normalise the URL so back/forward doesn't bounce off a dead hash.
  if (route.name === 'lists' && location.hash !== '' && location.hash !== '#/') {
    history.replaceState(null, '', '#/');
  }

  state.route = route;
  if (route.name === 'detail') openDetail(route.listId);
  else if (state.lists.status === 'idle') loadLists();
  notify();
}

function render() {
  const isDetail = state.route.name === 'detail';

  // Both views stay in the document so the composers, the row editor and the
  // aria-live region survive navigation.
  viewLists.hidden = isDetail;
  viewDetail.hidden = !isDetail;
  // Same-size boxes trading places, so the title stays put.
  backLink.hidden = !isDetail;
  appMark.hidden = isDetail;
  formNewList.hidden = isDetail;
  // No point offering "add an item" for a list that failed to load.
  formNewItem.hidden = !isDetail || state.detail.status === 'error';

  const title = isDetail ? (state.detail.list?.title ?? 'List') : 'Todos v2';
  appTitle.textContent = title;
  document.title = isDetail ? `${title} · Todos v2` : 'Todos v2';

  if (isDetail) detail.render();
  else lists.render();

  // Without this, keyboard and screen-reader users are stranded at the top of a
  // page whose content silently swapped underneath them.
  if (focusHeadingNext) {
    focusHeadingNext = false;
    appTitle.focus();
  }
}

feedback.mount();
lists.mount();
detail.mount();

subscribe(render);

window.addEventListener('hashchange', () => {
  focusHeadingNext = true;
  handleRoute();
});

window.addEventListener('online', retryCurrentView);

handleRoute();

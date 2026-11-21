import { demoSource } from './data.js';
import { state, filtered, resetSelection, setMode } from './state.js';
import { header, grid, detail } from './view.js';
import { settings } from './settings.js';

const shell = document.querySelector('.shell');
const workspace = document.querySelector('#workspace');
const closed = document.querySelector('#closed');
const dialog = document.querySelector('#confirm');
const status = document.querySelector('#hint');
let displayed = [];
let pendingRequest;

function viewModel() {
  if (state.mode === 'inventory') return { lists: [demoSource.inventory()], titles: ['Itens pessoais'] };
  if (state.mode === 'storage') return { ...demoSource.storage(state.context, state.tab),
    titles: ['Inventário do jogador', 'Armazenamento'] };
  const shop = demoSource.trade(state.context, state.direction);
  return { ...shop, lists: [shop.rows], titles: [state.direction === 'buy' ? 'Produtos' : 'Itens aceitos na venda'] };
}
function render(restore) {
  const scroll = [...workspace.querySelectorAll('[data-scroll]')].map(x => x.scrollTop);
  workspace.hidden = !state.open;
  closed.hidden = state.open;
  document.querySelector('#close').hidden = !state.open;
  if (!state.open) {
    closed.querySelector('button').focus();
    return;
  }
  const model = viewModel();
  const categories = ['Todos', ...new Set(model.lists.flat().map(row => row.category))];
  displayed = model.lists.map(filtered);
  displayed.forEach((rows, side) => {
    if (!rows.some(row => row.id === state.selected[side])) state.selected[side] = rows[0]?.id ?? null;
  });
  workspace.innerHTML = header(state, model.title, model.tabs, categories) +
    '<div class="body ' + (state.mode === 'storage' ? 'dual' : '') + '">' +
    displayed.map((rows, side) => grid(rows, side, state.selected[side], model.titles[side])).join('') +
    detail(selectedRow(), state.mode, state.side, state.direction) + '</div>';
  workspace.querySelectorAll('[data-scroll]').forEach((x, i) => { x.scrollTop = scroll[i] ?? 0; });
  if (restore?.id === 'search') {
    workspace.querySelector('#search').replaceWith(restore);
    restore.focus({ preventScroll:true });
  } else if (restore?.id) {
    document.getElementById(restore.id)?.focus({ preventScroll:true });
  } else if (restore?.dataset) {
    const entries = Object.entries(restore.dataset);
    [...workspace.querySelectorAll('button')].find(x =>
      entries.every(([k,v]) => x.dataset[k] === v))?.focus({ preventScroll:true });
  }
}
function selectedRow() {
  return displayed[state.side]?.find(row => row.id === state.selected[state.side]);
}
function closePanel() {
  state.open = false;
  status.textContent = '';
  render();
}
function openMode(mode) {
  setMode(mode);
  status.textContent = '';
  render();
  workspace.querySelector('nav [aria-pressed="true"]')?.focus({ preventScroll:true });
}
function completePreview() {
  if (!pendingRequest) return;
  status.textContent = 'Simulação concluída. Nenhum saldo ou item foi alterado.';
  pendingRequest = undefined;
}
function previewRequest(action) {
  const row = selectedRow();
  if (!row) return;
  const input = document.getElementById('amount');
  if (input && !input.reportValidity()) return;
  const amount = input ? input.valueAsNumber : 1;
  const labels = { use:'Usar', discard:'Descartar', transfer: state.side ? 'Retirar' : 'Depositar', buy:'Comprar', sell:'Vender' };
  pendingRequest = Object.freeze({ action, mode:state.mode, context:state.context, id:row.id, amount });
  document.querySelector('#confirmation').textContent = labels[action] + ' · ' + row.name +
    (input ? ' × ' + amount.toLocaleString(settings.language) : '') + '. Os dados da demonstração permanecerão iguais.';
  if (settings.confirmActions) {
    dialog.returnValue = 'cancel';
    dialog.showModal();
  } else completePreview();
}
dialog.addEventListener('close', () => {
  if (dialog.returnValue === 'confirm') completePreview();
  pendingRequest = undefined;
});
shell.addEventListener('click', e => {
  const button = e.target.closest('button');
  if (!button || button.disabled) return;
  const { mode, tab, direction, category, item, side, action } = button.dataset;
  if (button.id === 'close') return closePanel();
  if (mode) return openMode(mode);
  if (action) return previewRequest(action);
  if (item) {
    state.side = Number(side);
    state.selected[state.side] = item;
  } else {
    if (tab || direction) { state.category = 'Todos'; resetSelection(); }
    if (tab) state.tab = tab;
    if (direction) state.direction = direction;
    if (category) state.category = category;
  }
  render(button);
});
workspace.addEventListener('change', e => {
  if (e.target.id !== 'context') return;
  state.context = e.target.value;
  state.category = 'Todos';
  resetSelection();
  render(e.target);
});
workspace.addEventListener('input', e => {
  if (e.target.id !== 'search' || e.isComposing) return;
  state.query = e.target.value;
  render(e.target);
});
shell.addEventListener('keydown', e => {
  if (dialog.open || e.defaultPrevented || e.isComposing) return;
  const editable = e.target.matches('input, select, textarea, [contenteditable]');
  if (e.key === 'Escape' && state.open && !e.target.matches('select')) {
    e.preventDefault();
    closePanel();
  } else if (!editable && !e.repeat && !e.altKey && !e.ctrlKey && !e.metaKey && e.key.toLowerCase() === settings.toggleKey) {
    if (!state.open) openMode('inventory');
    else if (state.mode === 'inventory') closePanel();
  } else if (!editable && ['ArrowUp','ArrowDown','ArrowLeft','ArrowRight'].includes(e.key)) {
    const card = e.target.closest('[data-item]');
    if (!card && e.target.closest('button')) return;
    const side = Number(card?.dataset.side ?? state.side);
    const cards = [...workspace.querySelectorAll('[data-side="' + side + '"]')];
    if (!cards.length) return;
    const columns = getComputedStyle(cards[0].parentElement).gridTemplateColumns.split(' ').length;
    const offsets = { ArrowUp:-columns, ArrowDown:columns, ArrowLeft:-1, ArrowRight:1 };
    const index = cards.indexOf(card);
    const next = index < 0 ? cards[0] : cards[index + offsets[e.key]];
    if (next) { e.preventDefault(); next.focus(); }
  }
});
document.documentElement.style.setProperty('--cyan', settings.cyan);
document.documentElement.style.setProperty('--magenta', settings.magenta);
document.documentElement.classList.toggle('animate', settings.enableAnimations);
render();
workspace.querySelector('nav [aria-pressed="true"]')?.focus({ preventScroll:true });

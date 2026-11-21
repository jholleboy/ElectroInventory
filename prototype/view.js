import { icon } from './icons.js';
import { settings } from './settings.js';
const number = new Intl.NumberFormat(settings.language, { maximumFractionDigits: 4 });
const currency = new Intl.NumberFormat(settings.language, { style: 'currency', currency: 'USD' });
export const quantity = row => row.quantity == null ? 'Não informado' :
  row.measure === 'dinheiro' ? currency.format(row.quantity) : number.format(row.quantity);
const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'})[c]);
const artwork = row => settings.showItemIcons ? icon(row.icon) : '';
const colors = { 'Bebidas':'aqua', 'Alimentos':'gold', 'Drogas':'pink', 'Equipamentos':'blue', 'Celulares':'violet', 'Objetos de valor':'gold', 'Armas':'pink' };
const tone = row => colors[row.category] || 'aqua';
export const modes = { inventory: 'Inventário', storage: 'Armazenamento', trade: 'Comércio' };
const options = { residence:'Imóvel', vehicle:'Veículo', retail:'Varejo', npc:'Comerciante', weapons:'Armas', vehicles:'Veículos', services:'Serviços' };
const button = (value, label, kind, isActive) =>
  '<button data-' + kind + '="' + esc(value) + '" aria-pressed="' + isActive + '">' + esc(label) + '</button>';
export function header(state, title, tabs, categories) {
  const contexts = state.mode === 'storage' ? ['residence','vehicle'] : ['retail','npc','weapons','vehicles','services'];
  const controls = state.mode === 'inventory' ? '' : '<div class="options">' +
    '<label>Contexto <select id="context">' + contexts.map(key => '<option value="' + key + '"' +
      (key === state.context ? ' selected' : '') + '>' + options[key] + '</option>').join('') + '</select></label>' +
    '<div class="segmented">' + (state.mode === 'storage' ? tabs.map(x => button(x, x, 'tab', x === state.tab)).join('') :
      button('buy','Comprar','direction',state.direction === 'buy') + button('sell','Vender','direction',state.direction === 'sell')) +
    '</div></div>';
  return '<nav class="tabs" aria-label="Modo">' + Object.entries(modes).map(([key,label]) => button(key,label,'mode',key === state.mode)).join('') +
    '</nav><div class="heading"><div><h1>' + modes[state.mode].toUpperCase() + '</h1>' +
    (title ? '<p>' + esc(title) + '</p>' : '') + '</div><label class="search"><span aria-hidden="true">⌕</span><input type="search" id="search" placeholder="Pesquisar por nome" aria-label="Pesquisar por nome" value="' + esc(state.query) + '"></label></div>' +
    controls + '<div class="filters" aria-label="Categorias">' + categories.map(x => button(x, x, 'category', x === state.category)).join('') + '</div>';
}
export function grid(rows, side, selection, title) {
  return '<section class="collection"><div class="section-title"><h2>' + esc(title) + '</h2><small>' + rows.length +
    (rows.length === 1 ? ' entrada' : ' entradas') + '</small></div><div class="grid" data-scroll="' + side + '">' +
    (rows.length ? rows.map(row => '<button class="card ' + tone(row) + '" data-item="' + esc(row.id) + '" data-side="' + side +
      '" aria-pressed="' + (row.id === selection) + '"><span class="art">' + artwork(row) + '</span><span class="item-meta">' +
      '<span>' + esc(row.category) + '</span><strong>' + esc(row.name) + '</strong></span><span class="count">' +
      (row.price == null ? quantity(row) : currency.format(row.price)) + '</span></button>').join('') :
      '<p class="empty">Nenhum item nesta seleção.</p>') + '</div></section>';
}
export function detail(row, mode, side, direction) {
  if (!row) return '<aside class="detail"><p class="empty">Selecione um item para ver os detalhes.</p></aside>';
  const actions = mode === 'inventory' ? [
    ['use','Usar', !row.use, row.restriction ?? 'Uso indisponível neste contexto.'], ['discard','Descartar',false]
  ] : mode === 'storage' ? [['transfer', side ? 'Retirar' : 'Depositar', false]] :
    [[direction, direction === 'buy' ? 'Comprar' : 'Vender', Boolean(row.restriction) || row.quantity === 0, row.restriction ?? 'Sem estoque disponível.']];
  const divisible = row.measure !== 'unidade' && row.measure !== 'dinheiro';
  const amount = mode === 'storage'
    ? '<label class="amount">Quantidade <input id="amount" type="number" inputmode="decimal" min="' + (divisible ? '0.0001' : '1') + '" max="' +
      row.quantity + '" step="' + (divisible ? 'any' : '1') + '" value="' + Math.min(1, row.quantity) + '" required></label>' : '';
  return '<aside class="detail"><div class="detail-top"><h2>Detalhes do item</h2><div class="hero ' + tone(row) + '">' + artwork(row) +
    '</div><span class="tag">' + esc(row.category) + '</span><h3>' + esc(row.name) + '</h3><p>' + esc(row.description) +
    '</p><dl><dt>' + (mode === 'trade' ? 'Disponibilidade' : 'Quantidade') + '</dt><dd>' + quantity(row) + '</dd>' +
    (row.price == null ? '' : '<dt>Preço</dt><dd>' + currency.format(row.price) + '</dd>') +
    (row.state ? '<dt>Estado</dt><dd>' + esc(row.state) + '</dd>' : '') + '</dl>' +
    (row.restriction ? '<p class="restriction">' + esc(row.restriction) + '</p>' : '') + '</div><div class="detail-actions">' +
    amount + actions.map(([id,label,disabled,reason]) => '<button class="' + (id === 'discard' ? 'secondary' : 'primary') +
      '" data-action="' + id + '"' + (disabled ? ' disabled title="' + esc(reason) + '"' : '') + '>' + label + '</button>' +
      (disabled ? '<small>' + esc(reason) + '</small>' : '')).join('') + '</div></aside>';
}

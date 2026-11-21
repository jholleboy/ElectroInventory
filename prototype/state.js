export const state = {
  open: true, mode: 'inventory', context: 'residence', direction: 'buy',
  tab: 'Itens', category: 'Todos', query: '', side: 0, selected: [null, null]
};
export function resetSelection() {
  state.selected = [null, null];
  state.side = 0;
}
export function setMode(mode) {
  Object.assign(state, { mode, open: true, context: mode === 'trade' ? 'retail' : 'residence',
    direction: 'buy', tab: 'Itens', category: 'Todos', query: '' });
  resetSelection();
}
const normalize = s => s.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();
export function filtered(rows) {
  return rows.filter(row => (state.category === 'Todos' || row.category === state.category)
    && normalize(row.name).includes(normalize(state.query.trim())));
}

// Fixture presentation data. The future bridge supplies the same read-only shapes.
const row = (id, name, category, icon, quantity, description, extra = {}) =>
  Object.freeze({ id, name, category, icon, quantity, description, ...extra });
const personal = [
  row('water', 'Água mineral', 'Bebidas', 'bottle', 2.5, 'Garrafa de água para recuperar a sede.', { state: 'Unidade aberta', use: true }),
  row('soda', 'Sprunk', 'Bebidas', 'can', 4, 'Refrigerante cítrico em lata.', { use: true }),
  row('coffee', 'Café', 'Bebidas', 'cup', 1, 'Café quente em copo descartável.', { use: true }),
  row('bread', 'Pão branco', 'Alimentos', 'food', 3, 'Extra branco, de sabor suave.', { use: true }),
  row('burger', 'Hambúrguer', 'Alimentos', 'burger', 2, 'Refeição pronta para consumo.', { use: true }),
  row('cereal', "Crackles O’ Dawn", 'Alimentos', 'box', 1, 'Caixa de cereal matinal.', { use: true }),
  row('med', 'Analgésico', 'Drogas', 'pill', 2, 'Medicamento para uso no contexto permitido.', { use: true }),
  row('armor', 'Colete leve', 'Equipamentos', 'armor', 1, 'Proteção corporal. O estado equipado pertence ao jogo.', { use: true }),
  row('torch', 'Lanterna', 'Equipamentos', 'torch', 1, 'Equipamento portátil para iluminar áreas escuras.', { use: true }),
  row('pliers', 'Alicate', 'Equipamentos', 'tool', 1, 'Ferramenta para atividades compatíveis.', { restriction: 'Requer uma atividade compatível.' }),
  row('phone', 'Celular descartável', 'Celulares', 'phone', 1, 'Aparelho usado para chamadas e contatos.', { use: true }),
  row('watch', 'Relógio de pulso', 'Objetos de valor', 'watch', 2, 'Objeto de valor que pode ser negociado com compradores compatíveis.')
];
const stored = [
  row('stored-water', 'Água mineral', 'Bebidas', 'bottle', 1.25, 'Garrafa parcialmente usada.'),
  row('stored-bread', 'Pão branco', 'Alimentos', 'food', 5, 'Estoque do imóvel.'),
  row('stored-coffee', 'Café', 'Bebidas', 'cup', 2, 'Estoque do imóvel.')
];
const arms = [row('pistol', 'Pistola', 'Armas', 'weapon', 1, 'Arma armazenada separadamente das pilhas.', { measure: 'unidade', state: '12 munições' })];
const cash = amount => [row('cash', 'Dinheiro em espécie', 'Dinheiro', 'cash', amount, 'Saldo em espécie neste contexto.', { measure: 'dinheiro' })];
const offer = (item, price, available, extra = {}) => Object.freeze({ ...item, price, quantity: available, ...extra });
const storage = {
  residence: { title: 'Apartamento em Strawberry', tabs: ['Itens', 'Armas', 'Dinheiro'], items: stored, weapons: arms, cash: cash(650) },
  vehicle: { title: 'Armazenamento do veículo', tabs: ['Itens', 'Armas', 'Dinheiro'], items: [stored[0]], weapons: [], cash: cash(80) }
};
const trade = {
  retail: { title: '24/7 Supermarket', buy: [offer(personal[0], 3, 12), offer(personal[1], 4, 8), offer(personal[3], 5, 7), offer(personal[4], 9, null), offer(personal[5], 7, 6)], sell: [] },
  npc: { title: 'Comerciante de Strawberry', buy: [offer(personal[8], 48, 2)], sell: [offer(personal[11], 35, 2)] },
  weapons: { title: 'Ammu-Nation', buy: [offer(arms[0], 450, 1, { restriction: 'Requer licença na sessão simulada.' })], sell: [] },
  vehicles: { title: 'Concessionária', buy: [row('primo', 'Albany Primo', 'Veículos', 'car', 1, 'Oferta individual com cor e entrega definidas pela sessão.', { price: 5800 })], sell: [] },
  services: { title: 'Hospital', buy: [row('care', 'Atendimento médico', 'Serviços', 'pill', null, 'Atendimento. O resultado é um serviço.', { price: 180 })], sell: [] }
};
export const demoSource = {
  inventory: () => personal,
  storage: (context, tab) => {
    const target = storage[context];
    const lists = { Itens: [personal, target.items], Armas: [arms, target.weapons], Dinheiro: [cash(320), target.cash] };
    return { title: target.title, tabs: target.tabs, lists: lists[tab] };
  },
  trade: (context, direction) => ({ title: trade[context].title, rows: trade[context][direction] })
};

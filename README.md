# ElectroInventory — auditoria V1

Projeto independente para GTA V Legacy. Esta entrega é a Fase 1, baseada no Los Santos RED PT-BR RC2 em `ba4115b`. O ElectroHud é referência de estilo, diagnóstico e comunicação entre plugins. A implementação futura usará C#, .NET Framework 4.8, SDK oficial do RAGE Plugin Hook e MSBuild. A distribuição prevista é `Plugins/ElectroInventory.dll` e `Plugins/ElectroInventory/`, com scripts BUILD.ps1 e PACKAGE.ps1 nas fases de implementação.

- [Auditoria de abertura, armazenamento, comércio e persistência](INVENTORY_AUDIT.md)
- [Tipos, propriedades e ações](ITEM_TYPES.md)
- [Proposta de integração e decisões pendentes](INTEGRATION.md)
- [Especificação visual e controles](UI_SPEC.md)
- [Testes de aceitação](TESTING.md)
- [Índice de trechos da RC2](SOURCE_INDEX.md)

A tecla prevista para inventário pessoal é **I**. Armazenamento e comércio serão oferecidos a partir da interação original correspondente. O protótipo visual é a próxima fase e depende de aprovação.

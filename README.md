# ElectroInventory — protótipo visual V1

Projeto independente para GTA V Legacy. A auditoria da Fase 1 é baseada no Los Santos RED PT-BR RC2 em `ba4115b`. O ElectroHud é referência de estilo, diagnóstico e comunicação entre plugins. A implementação futura usará C#, .NET Framework 4.8, SDK oficial do RAGE Plugin Hook e MSBuild. A distribuição prevista é `Plugins/ElectroInventory.dll` e `Plugins/ElectroInventory/`, com scripts BUILD.ps1 e PACKAGE.ps1 nas fases de implementação.

- [Auditoria de abertura, armazenamento, comércio e persistência](INVENTORY_AUDIT.md)
- [Tipos, propriedades e ações](ITEM_TYPES.md)
- [Proposta de integração e decisões pendentes](INTEGRATION.md)
- [Especificação visual e controles](UI_SPEC.md)
- [Testes de aceitação](TESTING.md)
- [Índice de trechos da RC2](SOURCE_INDEX.md)

## Executar o protótipo

No PowerShell, a partir da raiz do repositório, com Python 3:

```powershell
py -3 -m http.server 8080 --bind 127.0.0.1 --directory prototype
```

Abra `http://127.0.0.1:8080/` no navegador. Os módulos JavaScript são servidos diretamente e dispensam empacotamento. Esta entrega roda no navegador e é independente do SDK do RAGE Plugin Hook. A versão GTA V, a compilação com o SDK oficial e o teste dentro do jogo ficam pendentes.

O cabeçalho marca os dados demonstrativos. As abas permitem experimentar inventário pessoal, armazenamento de imóvel ou veículo e compra/venda em diferentes contextos. No protótipo, I funciona somente com foco na interface e abre ou fecha o inventário pessoal. Em integração futura, armazenamento e comércio serão abertos exclusivamente pelos fluxos originais do LS RED.

## Estrutura

- `prototype/data.js` fornece os dados demonstrativos.
- `prototype/state.js` mantém estado, filtros e seleção.
- `prototype/view.js` e `styles.css` renderizam a interface.
- `prototype/app.js` trata navegação e solicitações locais de simulação.
- `prototype/icons.js` e `settings.js` definem ícones e opções visuais.

A Fase 3 depende de aprovação. O próximo passo técnico será implementar um provedor de leitura e adaptar a apresentação para snapshots autoritativos da RC2, incluindo identidade dos itens, permissões, revisões, bloqueios de abertura e encerramento da sessão.

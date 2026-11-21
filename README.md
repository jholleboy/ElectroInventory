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

O protótipo web continua disponível para comparação visual. A Fase 4 depende de aprovação.

## Fase 3 — plugin dentro do jogo

No Windows, instale o Developer Pack .NET Framework 4.8, Visual Studio Build Tools com MSBuild e o RAGE Plugin Hook compatível com GTA V Legacy. Coloque o SDK oficial em `<GTA V>\SDK\RagePluginHookSDK.dll`. Em PowerShell:

```powershell
.\BUILD.ps1 -GtaVDirectory 'C:\Games\Grand Theft Auto V'
.\PACKAGE.ps1
```

Extraia `artifacts\ElectroInventory-Legacy.zip` na raiz do GTA V. O ZIP instala `Plugins\ElectroInventory.dll` e `Plugins\ElectroInventory\Settings.xml`. O patch da RC2 é distribuído separadamente dentro do ZIP e precisa ser aplicado e compilado no checkout da RC2. Use uma cópia limpa de `ba4115b`. [Instruções de integração](INTEGRATION.md)

Carregue a RC2 integrada e o ElectroInventory em modo história. **I** abre/fecha o inventário pessoal quando a entrada estiver livre; **Esc** sai da pesquisa ou fecha a interface. Use **/** para pesquisar, setas ou mouse para selecionar, e PgUp/PgDn ou roda para paginar. **F10** abre/fecha a inspeção passiva de armazenamento ou comércio durante um menu compatível da RC2. Ctrl+F7/F8 pagina e Ctrl+F6 alterna o lado do armazenamento. Configure as duas teclas de abertura em `Settings.xml` se houver conflito.

`Live` usa dados da ponte ou indica indisponibilidade no log e mantém a tela fechada. `Demo` é um modo explícito de tela pessoal com um item fictício. Para a demonstração completa, use o protótipo web. A Fase 3 não habilita transações nem troca menus do jogo.

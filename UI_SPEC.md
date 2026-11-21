# Especificação visual

Painel central escuro translúcido, detalhes ciano e magenta, proporção de referência 1920×1080, área segura, rolagem em grades e foco visível. Modos e componentes compartilhados:

- **Inventário**: grade à esquerda e detalhe à direita, filtros por tipo/subtipo e pesquisa pelo nome traduzido. Mostrar contagem de pilhas ou unidades com legenda precisa. Ações no rodapé conforme executor e estado.
- **Armazenamento**: inventário do jogador à esquerda e contêiner à direita, cada qual com seleção independente. Abas para itens, armas e dinheiro conforme permissões. Quantidade e direção antes de confirmar, com atualização dos dois lados. Arrastar é opção condicionada à validação do executor.
- **Comércio**: nome e contexto da loja, categorias, grade de ofertas, detalhes, preço, saldo e estoque quando informados. Alternar compra/venda conforme a sessão e confirmar com cotação da RC2. Previews de serviços e veículos mantêm seus fluxos.

I abre/fecha apenas inventário pessoal. Esc fecha a camada com foco, sem consumir input de menus com prioridade. Navegação por mouse, teclado, seleção, scroll, busca e atalhos contextuais. Capturar só os controles necessários enquanto houver foco confirmado. Restaurar ao fechar, perder sessão ou descarregar. Armazenamento/comércio entram exclusivamente pelas interações originais.

PT-BR padrão, fallback inglês e catálogo próprio para textos exclusivos. Configuração futura em `Plugins/ElectroInventory/Settings.xml` com Enabled, Language, ToggleKey=I, Scale, Opacity, ShowItemIcons, ShowDescriptions, EnableSearch, EnableAnimations, ConfirmDestructiveActions, Diagnostics, Cyan e Magenta.

# Especificação visual e navegação

Painel central escuro translúcido, detalhes ciano e magenta, proporção de referência 1920×1080, área segura, rolagem em grades e foco visível. Modos e componentes compartilhados:

- **Inventário**: grade à esquerda e detalhe à direita, filtros por tipo/subtipo e pesquisa pelo nome traduzido. Mostrar contagem de pilhas ou unidades com legenda precisa. Ações no rodapé conforme executor e estado.
- **Armazenamento**: inventário do jogador à esquerda e contêiner à direita, cada qual com seleção independente. Abas para itens, armas e dinheiro conforme permissões. Quantidade e direção antes de confirmar, com atualização dos dois lados. Arrastar é opção condicionada à validação do executor.
- **Comércio**: nome e contexto da loja, categorias, grade de ofertas, detalhes, preço, saldo e estoque quando informados. Alternar compra/venda conforme a sessão e confirmar com cotação da RC2. Previews de serviços e veículos mantêm seus fluxos.

I abre/fecha apenas inventário pessoal. Esc fecha a camada com foco, sem consumir input de menus com prioridade. Navegação por mouse, teclado, seleção, scroll, busca e atalhos contextuais. Capturar só os controles necessários enquanto houver foco confirmado. Restaurar ao fechar, perder sessão ou descarregar. Armazenamento/comércio entram exclusivamente pelas interações originais.

PT-BR padrão, fallback inglês e catálogo próprio para textos exclusivos. Configuração futura em `Plugins/ElectroInventory/Settings.xml` com Enabled, Language, ToggleKey=I, Scale, Opacity, ShowItemIcons, ShowDescriptions, EnableSearch, EnableAnimations, ConfirmDestructiveActions, Diagnostics, Cyan e Magenta.


## Protótipo de navegador

- Clique nas abas para alternar o modo. Nos modos Armazenamento e Comércio, use **Contexto** para escolher a demonstração.
- Use Tab para chegar à grade. As setas movem o foco entre cartões e Enter seleciona. A roda do mouse rola cada grade. A busca aceita nome com ou sem acentos.
- Em Armazenamento, o cartão selecionado define a direção da transferência. As abas Itens, Armas e Dinheiro usam listas separadas. É possível simular uma fração em itens comuns; armas e dinheiro usam passos inteiros. A capacidade aparece apenas se informada pela fonte. A demonstração não atribui capacidade aos contêineres.
- Em Comércio, troque Comprar/Vender. O cartão apresenta o preço e o detalhe mostra disponibilidade, inclusive “Não informado”. O contexto de armas demonstra uma restrição de licença.
- Confirme ou cancele pelo diálogo. Esc cancela primeiro o diálogo, ou fecha o painel com foco quando o diálogo está fechado. I é local ao painel, cede aos campos editáveis e nunca registra um atalho do sistema. A validação da precedência com VehicleShowcase e outros menus pertence à integração.

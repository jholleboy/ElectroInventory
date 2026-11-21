# Testes do ElectroInventory

## Fase 2, navegador

Executado em Chromium a 1920×1080, 1280×800, 3440×1440 e 390×844. A verificação automatizada cobriu ausência de overflow horizontal nos três modos, busca por “agua”, navegação por setas e Enter, troca de contextos, ação bloqueada, quantidade acima do disponível, fração aceita, confirmação sem mutação, venda, cancelamento com Esc e reabertura com I. Capturas a 1920×1080 foram geradas para os três modos.

Para repetir manualmente, use o comando do README e experimente imóvel/veículo, abas Armas e Dinheiro e todos os contextos de Comércio. Reduza a altura para conferir a rolagem. Os saldos e as pilhas devem permanecer iguais depois de confirmar ou cancelar.

## Aceitação futura no GTA V

1. Em GTA V Legacy, compare radial original com I e Esc. Cubra O, Alt+N, mouse4, controller, telefone, pausa, showcase, algemas, atividade e carregamento. Verifique outros plugins e perfis com teclas personalizadas.
2. Compare consumo normal e lento, efeitos, cancelamento de descarte, uma unidade parcial e atualização de quantidade. Confira tradução e nomes internos preservados.
3. Deposite/retire em residência, armário filtrado e negócio. Teste proibição por tipo, última fração, duas ações repetidas, interrupção de animação e alteração simultânea por payout.
4. Entre pelo menu do veículo de dentro e de fora. Compare porta, alcance, velocidade, fechamento, mudança de veículo, perda de acesso, apreensão e carregamento de save. Teste armas, munição e dinheiro separadamente.
5. Compare varejo, NPC, estoque finito, preço variável, saldo insuficiente, consumo na compra, venda e quantidade. Cubra arma/munição, roupa, veículo, serviços e cancelamento de preview.
6. Injete em ambiente de teste mensagens obsoletas e duplicadas, mudança de sessão entre seleção e confirmação, respostas perdidas e descarga. O resultado deve ser no máximo uma alteração válida e fallback sem dois menus.
7. Confira 1080p, 16:10 e ultrawide, pesquisa, foco, rolagem e convivência com ElectroHud. Compare saves antes e depois pelo fluxo normal do jogo.

## Fase 3 — leitura no GTA V Legacy

1. Em .NET 8, execute `dotnet run --project tests/ProtocolTests.csproj` para verificar o contrato independente do SDK. Em Windows, compile e empacote com os scripts do README e confirme a instalação da RC2 com o patch.
2. Em Live, entre com RC2 indisponível e pressione I. Confira o log e a ausência de dados fictícios. Inicie/descarregue a RC2, troque de personagem e confira abertura, encerramento e restauração. Repita com inventário vazio, muitos itens, nomes repetidos e fração real.
3. Confira I/Esc, pesquisa incluindo a letra I, roda, setas e cursor. Use o VehicleShowcase e confirme que I continua com ele. Teste pausa, telefone, radial, algemas e menus. Compare o comportamento de F9 do ElectroHud e de F7 da inspeção.
4. Abra o recipiente real de um imóvel ou veículo. Pressione F7. Compare as quantidades, o acesso a armas/dinheiro e o fechamento ao sair do contexto. O menu original continua operável. Nenhuma entrada de transferência é emitida pela inspeção.
5. Faça o mesmo em varejo, NPC e em um contexto especializado. Compare preço por pacote e ofertas limitadas onde publicados, além do saldo. Confira indicação explícita quando o preço final só estiver no menu.
6. Teste resolução e sobreposições com ElectroHud, captura do cursor por frame e recuperação ao descarregar. Confira que o log não registra textos dos itens nem dados de saldo. Registre versão do jogo/RPH e o caminho em que houve falha.

Foram executados testes independentes do protocolo e análise de sintaxe C# 7.3. A compilação com o SDK oficial e os testes de renderização, entrada e coexistência dentro do jogo continuam pendentes.

### Regressões de cursor, Esc e F7

- Em 1080p, 16:10 e ultrawide, passe o cursor da borda externa até cada canto e centro do painel. Confira um cursor visível, alinhamento de clique, pesquisa, cartões e roda. Troque a resolução com o painel aberto.
- Feche com Esc e mantenha a tecla pressionada por um segundo. Solte e confira que a pausa fica fechada. Pressione Esc de novo para abrir a pausa normal. Repita com a pesquisa ativa, fechamento com I e controle conectado.
- Abra F7 em balcão normal, comércio com câmera que esconde o personagem, NPC e menu especializado sem catálogo. Compare os diagnósticos do cliente e de `Transaction.Update`. Repita com o menu original em confirmação.
- Entre no armazenamento de um veículo e alterne raiz, itens, armas e dinheiro. Saia do menu, mova o veículo e afaste-se. A inspeção deve acompanhar somente o contexto legítimo. Repita com ElectroHud V6 e confira que o menu original segue operável.

## Candidato interativo

Testes de console em `tests/ProtocolTests.csproj`: sessão ou revisão errada, token diferente, ação não autorizada ou composta, quantidade inválida, descarte e limpeza da confirmação ao mudar revisão, saldo e overflow de preço, preservação da fração, regressões do canal v1 e dos controles de Esc/cursor. O teste de revisão aposentada cobre o predicado que bloqueia um comando repetido. A execução do servidor sobre memória mapeada, efeitos da RC2 e ciclo completo no jogo requerem Windows.

Rodada final em save de teste:

1. Aplique o patch de ações, e opcionalmente ElectroHud V6, sobre `ba4115b` limpo. Compile a RC2 e o ElectroInventory com SDK oficial. Compare o cliente novo primeiro contra o patch antigo para verificar o fallback somente leitura.
2. Use comida, bebida e colete. Ative lanterna e binóculos. Confira seleção obsoleta, cancelamento, quantidades de descarte, troca de personagem no meio da animação, reabertura do inventário e valores finais.
3. Transfira unidades inteiras em um ponto sem animação com saldo fracionário. Compare ambas as pilhas. Teste restrição de tipo, menu fechado, veículo que se afasta e armazenamento com animação. Confira que armas e dinheiro seguem visíveis em leitura.
4. Compre e venda em transação genérica. Compare preço, dinheiro, estoque, unidade/pacote e saldo. Tente dois cliques rápidos e teste oferta sem estoque, mudança de preço e débito insuficiente. Verifique NPCs e lojas especializadas pelo menu original.
5. Confira F7 e Ctrl+F3 em menus originais, prioridade dos atalhos, troca de sessão, fechamento com I/Esc, cursor e coexistência com ElectroHud V6. Faça uma operação, salve pelo fluxo original e confira o estado após recarregar.

### Sessão Comprar/Vender

O console testa alternância entre abas com duas ou apenas uma direção, seleção identificada após atualização, preço alterado, saldo, estoque, item rejeitado, sessão inválida e revisão aposentada. As sequências compra/venda e venda/compra são modeladas com os contadores consumidos pelas regras de cotação. A execução dos menus reais continua pendente.

No teste integrado, abra F7 num comerciante comum que compre e venda. Use Ctrl+F2 mantendo a câmera e o menu nativo ativos. Compre um item elegível e confirme seu aparecimento em Vender; inverta a ordem e compare os contadores, saldos e pilhas com a RC2. Tente duplo acionamento, mudança de preço, última unidade, fechamento da sessão e comerciante indisponível. Repita em loja de direção única e em NPC com executor não habilitado.

### Mouse e devolução de foco

O console testa concessão por sessão, rejeição quando o contexto não é elegível, expiração, borda de clique e despacho de alvos para cartão, abas e confirmação. A suíte também cobre o mapeamento do cursor e Escape. Esses testes exercitam a lógica comum; o processamento do RAGENativeUI e o desenho do cursor devem ser verificados no GTA V.

No teste integrado, abra F7 em The Families Den e no armazenamento de um veículo ou imóvel. Clique em ambas as abas, em cartões e, onde houver oferta autorizada, em quantidade e confirmação/cancelamento. Confira que o menu nativo não recebe seleção ou ativação durante os cliques. Feche com F7 ou Esc, repita mantendo o clique pressionado e verifique a devolução do controle. Repita sem a ponte atualizada para confirmar o fallback passivo e com ElectroHud V6 ativo.

## Rodada integrada V3

A suíte de console cobre concessão e expiração, cliques em alvos, fechamento por Esc/F7, perda da sessão, troca de personagem, mapeamento em 720p/1080p/16:10/ultrawide, limites e frações, cotação, revisão e repetições. O teste de coordenadas usa a mesma função de escala do cliente. Os efeitos reais de transferência e comércio continuam pendentes no jogo.

1. Instale as duas DLLs compiladas a partir do V3 e do patch atualizado em RC2 limpa. Se usar ElectroHud, aplique seu patch V6 na mesma cópia da RC2.
2. Abra comércio e armazenamento pelo jogo e pressione F7. Confira que o menu nativo e seu banner somem apenas com concessão válida. Feche com F7 ou Esc e confira retorno ao mesmo menu e seleção. Teste descarregamento, perda de acesso, troca de personagem e fechamento externo.
3. Compare as grades e o cursor em 1080p e ultrawide. Navegue por filtros, busca, paginação, mouse, teclado e troca de lado. Confirme ícones padrão com pasta de imagens ausente e com arquivo inválido.
4. Em armazenamento, compare antes e depois nos dois lados, inclusive pilha fracionária. Em comércio genérico, compare preço, quantidade, total, saldo e atualização de ambas as abas após compra e venda. Tente clique duplo e cotação vencida. Repita em contextos passivos e especializados.

## Rodada integrada V3.1

Em save de teste e com ElectroHud V6, selecione Comprar ou Vender com NPC compatível, incluindo um NPC que só compra do jogador. Confirme a abertura automática da aba selecionada. Para fluxos especializados, confirme a continuidade do menu original. Depois teste o caminho comercial de estabelecimento, incluindo The Families Den se disponível.

No veículo, entre em Armazenamento pelo LS RED, tanto na raiz quanto nos detalhes de um item. Use setas, Enter, mouse, Esc e gamepad. O pool original não deve navegar nem aparecer durante a concessão e deve voltar no mesmo submenu ao fechar. Confira outro menu independente após devolver o foco. Teste perda de proximidade, movimento do carro e mudança de personagem.

Fora e dentro do veículo, guarde e retire unidades inteiras, inclusive de uma pilha parcialmente consumida. Compare os dois lados. Interrompa a animação após a confirmação e compare de novo. Repita com uma pilha abaixo de uma unidade, item bloqueado, clique duplo e troca de veículo durante a confirmação. Confira que armas e dinheiro continuam sob o menu original. Os testes de console exercitam as regras de árvore/pool, abas sem oferta executável, validação inteira e expiração, sem rodar a lógica nativa de GTA V.

## Rodada integrada V4

Os testes de console cobrem o novo payload com arma equipada, arma carregada sem munição, proteção, personagem/sessão incorretos, dados incompletos e leitura pelo codec existente. Também cobrem opções contextuais autorizadas, revisão que cancela confirmação, token inválido, ausência de executor de armas/dinheiro, unidades inteiras e resíduos. Os testes anteriores mantêm Esc, F7, foco, clique duplicado, expiração e devolução de entrada.

1. Em save de teste, compare F1 Equipamentos com arma equipada e colete da RC2. Troque de arma, dispare e mude de personagem. Verifique munição zero e lista sem armas.
2. Em veículo/imóvel, compare armas, munição, saldo e teto com o menu original. Use a saída explícita e verifique Guardar/Retirar, depósito e retirada pelas opções nativas. O overlay não emite essas operações especializadas.
3. Em itens comuns, use clique direito e a tecla Menu. Confirme que Informações não executa ação e que ações autorizadas exigem confirmação. Mude de sessão durante o menu, faça clique duplo e feche com Esc ou F7. Repita com NPC, comércio e ElectroHud V6.
4. Guarde e retire unidades inteiras de uma pilha com resíduo. Interrompa a animação de veículo depois da confirmação e compare as duas pontas. Confira proteção de contexto e foco ao descarregar o plugin e ao retornar ao menu original.

## Regressão no GTA Legacy: Bite! e NPCs

1. Abra Bite! e acione F7. Confira que a interface de refeições nativa mantém foco, inclusive para os combos de $8, $7 e $6. Faça uma compra em um save de teste e compare pagamento, efeitos de consumo, avanço de tempo e encerramento com a RC2 original.
2. Compare dinheiro em espécie e contas do personagem com a origem exibida pela ponte em um comércio comum e em um NPC que usa apenas espécie. Confira o saldo com dinheiro realmente insuficiente.
3. Negocie com Mackenzie Warren ou outro NPC com catálogo genérico. Abra Comprar/Vender, compare a aba inicial e confirme as duas direções. Confira animações, foco, alteração de saldo/estoque, retorno ao menu, afastamento antes da confirmação, pausa, NPC sem dinheiro, duplo clique e fechamento durante a fila.
4. Confira o fallback de arma, veículo, refeição e serviço. Os testes automatizados exercitam as regras de cotação, fundos, estoque e invalidação; não reproduzem animações ou assinaturas do SDK oficial.

### Atalhos do inventário

1. Com os controles livres, abra e feche o inventário pessoal com I. Abra novamente e feche com Esc. Pressione F10 com o inventário fechado e confirme que o LS RED recebe o atalho sem abrir o ElectroInventory.
2. Digite I na pesquisa do inventário e do comércio. Confira que a letra aparece no filtro. Abra o teclado nativo do GTA, o telefone, o VehicleShowcase, uma transação e os menus da RC2 e confirme que I não abre um segundo painel.
3. Use um `Settings.xml` antigo com `InspectKey=F10`. Confira que F7 assume a inspeção e Ctrl+F7 continua paginando. Altere `ToggleKey` e confira o novo atalho no rodapé.
4. Selecione Comprar e Vender com um NPC compatível sem pressionar I ou F7. Confirme a abertura da aba correspondente e o comportamento original das interações sem executor.

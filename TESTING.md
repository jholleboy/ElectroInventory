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
3. Confira I/Esc, pesquisa incluindo a letra I, roda, setas e cursor. Use o VehicleShowcase e confirme que I continua com ele. Teste pausa, telefone, radial, algemas e menus. Compare o comportamento de F9 do ElectroHud e de F10 da inspeção.
4. Abra o recipiente real de um imóvel ou veículo. Pressione F10. Compare as quantidades, o acesso a armas/dinheiro e o fechamento ao sair do contexto. O menu original continua operável. Nenhuma entrada de transferência é emitida pela inspeção.
5. Faça o mesmo em varejo, NPC e em um contexto especializado. Compare preço por pacote e ofertas limitadas onde publicados, além do saldo. Confira indicação explícita quando o preço final só estiver no menu.
6. Teste resolução e sobreposições com ElectroHud, captura do cursor por frame e recuperação ao descarregar. Confira que o log não registra textos dos itens nem dados de saldo. Registre versão do jogo/RPH e o caminho em que houve falha.

Foram executados testes independentes do protocolo e análise de sintaxe C# 7.3. A compilação com o SDK oficial e os testes de renderização, entrada e coexistência dentro do jogo continuam pendentes.

### Regressões de cursor, Esc e F10

- Em 1080p, 16:10 e ultrawide, passe o cursor da borda externa até cada canto e centro do painel. Confira um cursor visível, alinhamento de clique, pesquisa, cartões e roda. Troque a resolução com o painel aberto.
- Feche com Esc e mantenha a tecla pressionada por um segundo. Solte e confira que a pausa fica fechada. Pressione Esc de novo para abrir a pausa normal. Repita com a pesquisa ativa, fechamento com I e controle conectado.
- Abra F10 em balcão normal, comércio com câmera que esconde o personagem, NPC e menu especializado sem catálogo. Compare os diagnósticos do cliente e de `Transaction.Update`. Repita com o menu original em confirmação.
- Entre no armazenamento de um veículo e alterne raiz, itens, armas e dinheiro. Saia do menu, mova o veículo e afaste-se. A inspeção deve acompanhar somente o contexto legítimo. Repita com ElectroHud V6 e confira que o menu original segue operável.

## Candidato interativo

Testes de console em `tests/ProtocolTests.csproj`: sessão ou revisão errada, token diferente, ação não autorizada ou composta, quantidade inválida, descarte e limpeza da confirmação ao mudar revisão, saldo e overflow de preço, preservação da fração, regressões do canal v1 e dos controles de Esc/cursor. O teste de revisão aposentada cobre o predicado que bloqueia um comando repetido. A execução do servidor sobre memória mapeada, efeitos da RC2 e ciclo completo no jogo requerem Windows.

Rodada final em save de teste:

1. Aplique o patch de ações, e opcionalmente ElectroHud V6, sobre `ba4115b` limpo. Compile a RC2 e o ElectroInventory com SDK oficial. Compare o cliente novo primeiro contra o patch antigo para verificar o fallback somente leitura.
2. Use comida, bebida e colete. Ative lanterna e binóculos. Confira seleção obsoleta, cancelamento, quantidades de descarte, troca de personagem no meio da animação, reabertura do inventário e valores finais.
3. Transfira unidades inteiras em um ponto sem animação com saldo fracionário. Compare ambas as pilhas. Teste restrição de tipo, menu fechado, veículo que se afasta e armazenamento com animação. Confira que armas e dinheiro seguem visíveis em leitura.
4. Compre e venda em transação genérica. Compare preço, dinheiro, estoque, unidade/pacote e saldo. Tente dois cliques rápidos e teste oferta sem estoque, mudança de preço e débito insuficiente. Verifique NPCs e lojas especializadas pelo menu original.
5. Confira F10 e Ctrl+F3 em menus originais, prioridade dos atalhos, troca de sessão, fechamento com I/Esc, cursor e coexistência com ElectroHud V6. Faça uma operação, salve pelo fluxo original e confira o estado após recarregar.

### Sessão Comprar/Vender

O console testa alternância entre abas com duas ou apenas uma direção, seleção identificada após atualização, preço alterado, saldo, estoque, item rejeitado, sessão inválida e revisão aposentada. As sequências compra/venda e venda/compra são modeladas com os contadores consumidos pelas regras de cotação. A execução dos menus reais continua pendente.

No teste integrado, abra F10 num comerciante comum que compre e venda. Use Ctrl+F2 mantendo a câmera e o menu nativo ativos. Compre um item elegível e confirme seu aparecimento em Vender; inverta a ordem e compare os contadores, saldos e pilhas com a RC2. Tente duplo acionamento, mudança de preço, última unidade, fechamento da sessão e comerciante indisponível. Repita em loja de direção única e em NPC com executor não habilitado.

### Mouse e devolução de foco

O console testa concessão por sessão, rejeição quando o contexto não é elegível, expiração, borda de clique e despacho de alvos para cartão, abas e confirmação. A suíte também cobre o mapeamento do cursor e Escape. Esses testes exercitam a lógica comum; o processamento do RAGENativeUI e o desenho do cursor devem ser verificados no GTA V.

No teste integrado, abra F10 em The Families Den e no armazenamento de um veículo ou imóvel. Clique em ambas as abas, em cartões e, onde houver oferta autorizada, em quantidade e confirmação/cancelamento. Confira que o menu nativo não recebe seleção ou ativação durante os cliques. Feche com F10 ou Esc, repita mantendo o clique pressionado e verifique a devolução do controle. Repita sem a ponte atualizada para confirmar o fallback passivo e com ElectroHud V6 ativo.

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

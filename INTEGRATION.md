# Proposta de integração

O LS RED possui dados, sessões, validação e mutações. O ElectroInventory possui UI, seleção e entrada. Usar o padrão do ElectroHud de conexão local versionada e descoberta entre domínios do RPH, com canal separado por PID e instância. O protocolo do ElectroHud fica independente. A ponte será opcional. A comunicação local e um token de sessão não são uma garantia de segurança contra outro código executando dentro do processo.

| Mensagem | Contrato proposto |
| --- | --- |
| Hello / Ready | Versão maior/menor, recursos, instância e heartbeat. Diferenciar leitura, ações e confirmação visual. |
| Open | A RC2 cria uma sessão opaca em inventário pessoal autorizado ou em callback de abertura original. Inclui modo, rótulo do contexto, tipos habilitados, geração e validade. Nenhum comando aceita coordenadas, nome de loja, handle de veículo ou ID persistido para abrir acesso arbitrário. |
| Snapshot | Revisão, IDs opacos de itens/ofertas, rótulos localizados, quantidades/fracionamento, preço autoritativo, moeda/saldo, limites/estoque quando conhecidos e ações. Identificadores, textos, atributos opcionais e permissões são campos distintos. |
| Action | ID único da solicitação, sessão, revisão, ID da linha, ação permitida, quantidade e revisão da cotação. Revalidar acesso, identidade, disponibilidade, saldo, preço, contêiner e quantidade no fiber do LS RED. Quantidades positivas dentro dos limites atuais, sem aceitar preço do cliente. |
| Result / Refresh | Resultado autoritativo, falha/ocupado/obsoleto, ou aceito em andamento. Enviar a nova revisão ao efetivar. Deduplicar Action por sessão e ID; ao perder resposta, consultar resultado anterior em vez de reenviar com novo ID. |
| Visible / Close | Confirmar somente após desenho e aquisição de entrada da revisão certa. Encerrar e invalidar permissões ao perder acesso, sair de alcance, mudar de sessão, descarregar ou expirar heartbeat. |

Ponto de integração proposto dentro dos fluxos originais, perto das rotinas atualmente embutidas nos callbacks de menu. Adaptadores de itens, armas, dinheiro e famílias comerciais chamam a mesma operação que o menu nativo. Não disparar um delegate antigo com seleção ou scroller obsoletos. Se for preciso separar uma operação do callback, ambos os caminhos devem passar pelo mesmo executor. Na troca para a UI moderna, manter câmera, animação, limpeza e condições de vida do menu. O loop de veículo observa `MenuPool.IsAnyMenuOpen`, portanto a sessão visual precisa ser reconhecida ali durante a substituição, sem falsificar um menu aberto globalmente. Ao falhar, restaurar a navegação nativa somente enquanto o contexto continuar válido.

Toda operação exige revalidação no momento da execução, inclusive depois de yields que possam mudar a entidade, o acesso, a pilha ou os fundos. Revisões e deduplicação reduzem a chance de ação atrasada sobre pilha substituída ou de compra em dobro. A transferência precisa do executor original com política explícita para interrupção entre remoção/animação/adição e para a última unidade fracionária. Não expor arrastar e soltar até demonstrar que equivale à mesma ação validada.

## Decisões antes da implementação

1. Definir o ponto de handoff e retorno para radial, interior, veículo e comércio, sem disparar fechamento de câmera ou porta prematuramente.
2. Confirmar precedência de I/Esc, teclado remapeado, foco de pesquisa, telefone e controller numa instalação real. Reaproveitar o estado de menus do LS RED para coexistir com ElectroHud; documentar proposta separada se os sinais disponíveis forem insuficientes.
3. Escolher formato de mensagens, limites, fila e timeout; testar isolamento do RPH em leitura somente antes da escrita.
4. Escolher semântica de revisões, identificadores de pilhas/ofertas e resultado de operações assíncronas, em especial a fração final.
5. Dividir os adaptadores por família. Serviços, veículos, equipamentos e armas precisam de contratos próprios quando diferirem do varejo convencional.
6. Definir testes de concorrência e restauração antes da substituição. Brincar com dados fictícios só no protótipo visual e com indicação explícita.

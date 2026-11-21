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

## Ponte implementada na Fase 3

Use o patch `integration/LosSantosRED-ba4115b-electroinventory-read-v1.patch` a partir de uma cópia limpa em `ba4115b`:

```powershell
$patch = 'C:\src\ElectroInventory\integration\LosSantosRED-ba4115b-electroinventory-read-v1.patch'
git apply --check $patch
if ($LASTEXITCODE -ne 0) { throw 'Confira o checkout limpo da RC2 em ba4115b.' }
git apply $patch
```

Compile a cópia da RC2 pelo procedimento do repositório LS RED e instale sua DLL e seu catálogo PT-BR. A publicação foi preparada sem referência ao ElectroHud. Os patches ElectroInventory e ElectroHud V6 puderam ser aplicados juntos a uma cópia temporária. A coexistência em execução continua pendente.

O SDK do RPH oferece o evento RawFrameRender e texturas. Escolhemos GDI+ para rasterizar o painel em PNG temporário e o RPH para desenhar a textura em uma região central proporcional. Isso evita servidor HTTP, navegador embutido e outra dependência de UI. A textura é reutilizada quando os dados e a seleção não mudam. Foco, cursor e controles são de responsabilidade do plugin. A digitação em jogo aceita letras latinas e espaços, com busca insensível a acentos. O protótipo web retém o suporte do navegador a IME e edição completa. A área de detalhes tem altura fixa e pode abreviar textos extensos.

### Contrato de leitura 1

Canal `Local\ElectroInventory.Lsr.read.v1.<PID>`; mutex de tentativa imediata `.Lock`; mapa de 1 MiB. Cabeçalho little-endian tem magic `0x45494E31` no offset 0, versão no 4, PID no 8, comprimento no 12, timestamp monotônico no 16. O produtor invalida o timestamp antes de gravar e confirma por último, sob o mutex. O consumidor abre a visão com direitos somente leitura. Rejeita versão/PID inesperados, dados incompletos, tamanho fora do limite e snapshots com mais de dois segundos. Timestamp zero encerra a sessão. Um GUID distingue sessões e o handle distingue a troca de personagem.
O produtor e o consumidor convertem o `PoolHandle` do RPH para `uint` e reinterpretam seus 32 bits como `int` no campo existente. O bit mais alto é preservado e o protocolo continua na versão 1.

Payload inclui GUID, handle, modo, bloqueio de entrada, indicador de lista parcial, saldo opcional, título e até 2.000 linhas. Cada linha tem grupo, quantidade e preço double, ID original e textos UTF-8 delimitados por comprimento. NaN significa valor indisponível. Texto é limitado por campo; a interface identifica lista parcial. Grupos 0/1 são itens do jogador/contêiner, 2 armas de depósito, 3 dinheiro de depósito, 4 compra e 5 venda. Não há mensagem, canal ou executor de ações. Os indicativos de consumível e posse informam propriedades existentes, sem representar autorização atual para executar a ação.

### Cobertura e limites

- **Pessoal:** usa nome interno, tradução da RC2, categoria, descrição, fração e propriedades do item. O bloqueio lê menus, telefone, atividade, radial, algemas e VehicleShowcase. O cliente também verifica pausa, fade, cutscene e personagem atual. A entrega não registra I globalmente no Windows.
- **Imóveis:** leitura dos pontos internos autorizados via `Residence/Business.CreateInventoryMenu`. Só publica recipientes habilitados e aplica a precedência de filtros da RC2. O menu geral de administração do imóvel não abre uma sessão de inspeção. O limite de depósito de dinheiro documentado na auditoria permanece no menu original.
- **Veículos:** lê no submenu raiz de armazenamento ou em um dos seus três submenus imediatos, dentro das regras de acesso do veículo. Itens, armas e dinheiro permanecem grupos distintos. A inspeção fecha ao sair dos submenus suportados ou ao perder o acesso.
- **Comércio:** observa `Transaction.Update`, incluindo sessões válidas de NPC. Preço por pacote e disponibilidade são publicados quando a linha genérica fornece os dados. Armas, veículos e serviços aparecem com descrição e orientação ao menu original quando preço final/disponibilidade dependem de estado especializado. Lojas com menus fora de `Transaction`, como roupa e salão, permanecem fora desta inspeção.
- **Foco:** F10 é um visor passivo de diagnóstico para evitar concorrer com a interface original. Os teclados de paginação do visor são locais ao seu fluxo e não bloqueiam o menu original. A troca de interface e a captura completa de entrada em armazenamento e comércio pertencem à Fase 5.

## Cobertura da inspeção na RC2

| Origem | Publicação |
| --- | --- |
| Varejo que usa `GameLocation.StandardInteract` | O loop `ProcessTransactionMenu` chama `Transaction.Update`. Inclui os catálogos padrão de conveniência, bares, restaurantes, barracas, bebidas e penhores quando o local abre uma transação com ofertas. Um balcão ou local sem catálogo ativo não é suficiente. |
| Drive-thru, máquinas de venda, Ammu-Nation, concessionária, agências de polícia/bombeiros e hospital | Têm caminhos próprios que chamam `ProcessTransactionMenu` para suas transações. Ofertas especializadas ainda podem exigir o menu original para preço final ou disponibilidade. |
| NPC e comércio de gangue | `PersonTransaction` e a interação comercial de `GangDen` chamam `Update`. Conversa pausada, NPC indisponível, lay-low e confirmação nativa não produzem contexto comercial inspecionável. |
| Roupa, salão, oficina, jogos, falsificador e bombas de combustível | Menus especializados sem catálogo de `Transaction.Update` no fluxo em questão. Criar um objeto `Transaction` sem chamar o loop comercial não produz uma sessão. Ficam no menu original. |
| Imóvel | Pontos que usam `Residence/Business.CreateInventoryMenu`; só publica os recipientes permitidos pelo ponto. |
| Veículo | Menu raiz de armazenamento e submenus imediatos de itens, armas e dinheiro criados para o próprio veículo. Requer o menu original, propriedade ou entrada anterior, veículo válido, velocidade até 0,5, distância 2D até 7 m e jogador vivo/livre. Detalhes mais profundos de uma pilha/arma não estão cobertos. |

F10 aceita uma sessão válida mesmo que a câmera da transação oculte o pedestre. A identidade do pedestre ainda é conferida. O log do cliente diferencia ausência de snapshot, ausência de sessão de comércio/armazenamento e incompatibilidade de tela ou personagem; o produtor registra mudanças de estado comercial com classe do local e motivo, sem nome do jogador, itens, preços ou saldo.

O cursor do GTA continua ativo por frame para fornecer e atualizar os eixos 239/240. O cursor próprio é uma textura desenhada logo após o painel no `RawFrameRender`, somente quando o ponteiro está sobre a área opaca. Fora dela, o GTA desenha seu próprio cursor. O mesmo ponto em pixels alimenta o teste dos cliques. A inspeção passiva deixa o cursor com o menu original.

Ao atualizar no Windows, aplique o patch atualizado a uma cópia limpa da RC2 `ba4115b` (e aplique também V6 se for usar ElectroHud), recompile a RC2 e depois use `BUILD.ps1` e `PACKAGE.ps1` como no README. Instale as DLLs recompiladas.

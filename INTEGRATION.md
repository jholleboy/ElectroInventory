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

## Candidato interativo — Fases 4 a 7

Aplique `LosSantosRED-ba4115b-electroinventory-actions-v2.patch` diretamente à RC2 limpa, em vez do patch de leitura. A publicação v1 continua byte a byte no mesmo formato. O canal separado `Local\ElectroInventory.Lsr.actions.v2.<PID>`, com mapa de 64 KiB e mutex próprio, contém ofertas ligadas ao timestamp da publicação v1. Um cliente v1 continua a ler a mesma fonte, e um produtor com patch antigo permanece somente leitura para o novo cliente. Os canais do ElectroHud são independentes.

Cabeçalho v2: magic/PID (0/4), timestamp (8), revisão GUID (16), contagem (32), ID/status da resposta (40/56). O slot de escrita usa flag no offset 64, ID/sessão/revisão/token nos offsets 68/84/100/116, ação/quantidade em 132/136 e timestamp em 140. A partir de 160, cada posição corresponde à linha do snapshot v1 e guarda token, máscara e máximo em 24 bytes. O cliente não envia preço, identidade textual nem endereço de um recipiente. Revisões mudam quando o conteúdo ou os vínculos locais mudam; o servidor aposenta a revisão antes de chamar um executor. Uma repetição da mesma solicitação conserva a revisão anterior e é rejeitada. O cliente usa um pedido por vez, informa timeout ou resultado incerto e nunca reenvia automaticamente. A comunicação local não isola plugins hostis que já executam dentro do processo.

| Modo | Executores usados | Operações ainda indisponíveis |
| --- | --- | --- |
| Inventário pessoal | Ação natural do radial via `ActivityManager` e overrides de comida, bebida, colete, lanterna, binóculos, rádio e guarda-chuva. Descarte segue 1, 5 quando o radial permite, ou toda a pilha, com confirmação e verificação da mesma pilha no fim da animação. | Outros tipos de uso não validados, consumo lento em menus contextuais e equipar/desequipar armas como ações genéricas. Resultado assíncrono indica que a atividade começou; efeitos e cancelamento continuam sob controle da RC2. |
| Armazenamento | Executor compartilhado com `ModItem.CreateInventoryManageMenu` para itens em contextos originais sem animação. A ponte só transfere unidades inteiras quando ambas as pilhas podem preservar exatamente a fração restante. | Operações fracionárias finas, transferência com animação, armas e dinheiro. A RC2 não oferece um executor comum sem risco de interrupção para esses caminhos. |
| Comércio | `ModItem.PurchaseItem/SellItem` em transações de estabelecimento com executores genéricos e efeitos de loja base. A RC2 confere preço atual, saldo, estoque, limites, sessão, unidade/pacote e precisão antes de chamar a rotina. | NPCs, que fecham/reabrem o menu e usam animações próprias; consumo ao comprar, roubo, veículos, roupas, serviços, armas, overrides de loja e ofertas com mínimo ou passo especial. Permanecem disponíveis via menu original. |
| Interface | Ações contextuais, seleção de quantidade, confirmação, retorno, estado vazio, categorias, pesquisa pessoal, paginação e cursor do candidato. | O menu original continua a controlar a entrada nos contextos passivos. A seleção ali usa atalhos com Ctrl. Nenhum contexto é aberto remotamente pelo ElectroInventory. |

Se uma rotina original lançar exceção depois de iniciar, a resposta será incerta. Consulte o inventário antes de decidir repetir a operação. O cliente não tentará compensar alterações por conta própria. O pagamento, o estoque e a persistência pertencem à RC2.

### Comprar e vender na mesma sessão

`Transaction.CreateTransactionMenu` cria `PurchaseMenu` e `SellMenu` a partir do mesmo `ShopMenu`. Os menus mantêm seleções distintas, e a troca nativa pode esconder ou descartar o menu visual oposto. `ProcessTransactionMenu` chama `Update`, que publica um único contexto; o NPC também chama `Update` no próprio loop. O ElectroInventory filtra os grupos 4 e 5 do mesmo snapshot. A mudança de aba é local, sem chamadas a Show, Dispose ou callbacks de seleção, e mantém a transação, câmera e menu nativo ativos.

Com foco concedido, clique nas abas. Use **Ctrl+F2** para alternar pelo teclado, **Ctrl+↑/↓** para selecionar, **Ctrl+←/→** para ajustar e **Ctrl+F3** duas vezes para revisar e confirmar. Uma direção sem linhas elegíveis não pode ser selecionada. Linhas sem cotação confiável ou sem executor continuam consultáveis quando publicadas, mas sem botão de transação habilitado. Em comércio comum, Vender só recebe linhas aceitas pelo catálogo que tenham ao menos uma unidade inteira elegível. Após mudanças no estoque, saldo ou itens, o próximo snapshot atualiza ambas as abas sem trocar a aba corrente; confirmações antigas são descartadas pela mudança de revisão.

A RC2 revalida a existência do submenu construído para cada direção, a linha original do catálogo, a oferta e o contexto no momento da execução. Vender incrementa o contador de compras do comerciante; a ponte não aumenta o estoque de venda se a rotina original não o fizer. NPCs e especializações continuam somente leitura no ElectroInventory.

### Mouse em sessões de comércio e armazenamento

O cliente solicita foco separadamente da leitura e das ofertas de ação. O canal v2 reserva 49000 e 49024 para a solicitação e a concessão, cada uma com GUID de sessão e timestamp de 64 bits no deslocamento +16. O cliente só captura quando a RC2 confirma a sessão atual. A concessão dura até 750 ms após o último pedido válido, com margem de 100 ms no cliente. Patches anteriores ignoram esses campos e conservam o modo passivo.

A RC2 pausa temporariamente `ProcessLocalizedMenus` somente no `MenuPool` associado à sessão original. O estado e a câmera da interação permanecem ativos. Fechar a inspeção, perder o contexto ou expirar a concessão devolve o processamento ao menu original. No fechamento normal, uma pressão de mouse ainda ativa é drenada antes da devolução. Ao ganhar foco é necessário soltar um botão já pressionado antes do primeiro clique aceito. Os controles 237/238 são desabilitados por frame; os eixos 239/240 fornecem o mesmo ponto para os alvos e para o cursor desenhado depois do painel. A captura de Escape usa a mesma proteção do inventário pessoal, inclusive no frame de soltura.

Com foco, clique em cartões de ambos os lados, abas Comprar/Vender e controles de ação, quantidade, confirmação e cancelamento. F10 e os atalhos Ctrl continuam disponíveis. Um preço pode ser informativo sem que a RC2 publique uma ação autorizada para a linha; isso inclui fluxos especializados e NPCs sem executor compatível. A concessão do mouse não habilita nenhuma ação de jogo.

## Candidato V3

O menu nativo mantém `Visible` e sua seleção. A RC2 pausa `ProcessLocalizedMenus` apenas no pool concedido e protege também os callbacks `RawFrameRender` que desenham banners de local, NPC e táxi. Os loops de interação e `Transaction.Update` continuam ativos. Os callbacks de banner agora são removidos com a mesma referência registrada. O renderizador consulta apenas a concessão publicada, sem chamar verificações de gameplay nesse callback. Ao fechar, perder a sessão ou expirar a concessão, o pool volta ao processamento original. Contextos sem concessão continuam passivos. Os protocolos de leitura v1 e ações v2 permanecem no mesmo formato.

- Inventário: slots com quantidade, ícones genéricos e seleção. Pesquisa por nome, filtros por categoria e paginação com mouse, roda ou teclado. Home/End percorrem os filtros. Os textos de itens continuam vindo da RC2.
- Imagens locais opcionais: guarde PNGs de até 512×512 e 1 MiB em `Plugins\ElectroInventory\items`. O nome é o SHA-256 hexadecimal minúsculo do identificador interno em UTF-8, seguido de `.png`. Desative com `ItemImages=false`. Ausência ou erro usa o ícone padrão. O plugin carrega arquivos locais sob demanda.
- Armazenamento: seleção e página independentes nos dois lados. Ctrl+F6 alterna o lado por teclado. A capacidade genérica de itens continua ausente quando não existe na RC2. Na linha de dinheiro, o painel mostra o limite fornecido pelo armazenamento original em leitura. Depósito e retirada continuam vinculados aos executores autorizados.
- Comércio: exibe preço anunciado, estoque conhecido, saldo e total da quantidade selecionada. A interface trata o total como apresentação; os executores validam todas as cifras na RC2. Após retorno de sucesso, o cliente prioriza nova leitura dos snapshots e conserva a aba. NPCs e outras ofertas especializadas continuam somente leitura.

Ainda precisam de verificação no GTA V o comportamento visual dos banners, restauração da seleção nativa, fontes/ícones em resoluções reais e a coexistência de entrada com o ElectroHud.

## V3.1 — NPC, foco e armazenamento de veículos

- `PersonTransaction.Setup` passa o catálogo do NPC para `Transaction`. O construtor dos menus só cria a direção que o catálogo autoriza. Um NPC pode comprar do jogador sem vender. O fluxo de NPC fecha e reabre menus e executa animações próprias, por isso a V3.1 devolve F10 ao menu original com uma mensagem de ajuda quando a ponte reconhece esse contexto. `GangDen` usa também um caminho de estabelecimento, que permanece sujeito às verificações próprias de `Transaction`.
- `VehicleInteractionMenu.ProcessMenu` processa o pool original; seus submenus pertencem ao mesmo pool. A ponte verifica a árvore do armazenamento, incluindo seus detalhes, sem conceder acesso a portas, vidros ou outros ramos. Um pool diferente invalida a sessão. O cliente suprime controles nativos de frontend, incluindo navegação, confirmação e retorno nos grupos 0 e 2, somente enquanto possui foco. O hook da RC2 continua limitado ao pool concedido. A seleção e os valores nativos permanecem no objeto original.
- Na rua, o armazenamento veicular origina o menu com animações. A exclusão desse caso deixava as pilhas sem ofertas na V3. A V3.1 valida a pilha, a sessão, a distância, a direção, o tipo, a quantidade e a precisão, então executa o mesmo movimento inteiro do menu original sem yield entre retirada e crédito. Em seguida, o feedback original de animação é agendado apenas para o executor base e se a sessão ainda for válida. Interromper a animação depois do crédito não repete nem desfaz a transferência. A ponte não autoriza frações, armas, dinheiro ou animações especializadas.
- Ative `Diagnostics` para ver tipo de contexto, elegibilidade do pool, estado da concessão, contagem por direção, autorizações e motivos agregados. Os logs não incluem itens, saldo ou preços. O canal v2 usa dois campos antes reservados para habilitar esse diagnóstico e explicar a política de foco. O formato de leitura e os comandos existentes continuam compatíveis.

## V4 — Equipamentos e ações do item

Pressione F1 ou clique em **Equipamentos**, com foco concedido, para inspecionar a arma equipada, as armas carregadas, a munição reportada pelo GTA e os pontos de proteção. A lista lê `Character.Inventory.Weapons` e `EquippedWeapon`, usados pela RC2 em `WeaponEquipment` e no save, e o colete de `ArmorManager.EquippedArmorItem`. A leitura não chama `StoreWeapons`, que altera o estado do LS RED. Volte ao inventário para filtrar **Itens utilizáveis** pelo conjunto de ofertas v2 com ação Usar.

A extensão de equipamento é somente leitura no espaço reservado do mapa v2: timestamp em 49100, comprimento em 49108 e payload em 49112. Usa os campos do snapshot existente, com grupos próprios 0 proteção, 1 arma equipada e 2 arma carregada. É validada por tamanho, timestamp, GUID de sessão e personagem. Até 80 armas e uma linha de proteção são publicadas, dentro do limite do buffer. Clientes anteriores ignoram o bloco. Pontes anteriores não apresentam dados de equipamento.

O menu contextual abre com clique direito em um cartão ou com a tecla Menu do teclado. Use as setas e Enter para escolher. Informações apenas retorna aos detalhes. Escolher uma ação autorizada abre a confirmação e conserva a validação v2. Mudanças de revisão ou sessão invalidam a confirmação. Armas carregadas, armazenadas e dinheiro indicam o caminho de volta aos controles originais.

**Executores auditados.** `WeaponStorage.PlaceInStorage` remove a arma nativa depois de armazenar o objeto. `RemoveFromStorage` remove a entrada antes de `StoredWeapon.GiveToPlayer`, que restaura arma, munição e variação. Esses métodos não reportam confirmação atômica das duas pontas. Munição avulsa não é um executor de armazenamento independente nessa interface. `CashStorage.CreateInteractionMenu` valida dinheiro em espécie e um teto exclusivo e atualiza scrollers, animação e callbacks de `GameLocation`. A V4 exibe o saldo do recipiente e seu teto real, e deixa depósito e retirada de dinheiro no menu original. Frações continuam visíveis; a ponte transfere apenas unidades inteiras pelo fluxo já validado, preservando os resíduos. A animação veicular continua posterior à operação concluída, sem uma mutação tardia prevista pela ponte.

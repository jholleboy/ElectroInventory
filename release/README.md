# Operação do pipeline de release

A compilação fica no Windows self-hosted. O job de publicação roda no ambiente hospedado com permissão `contents: write`; validação e build usam `contents: read`. O pipeline nunca baixa o SDK, nem arquiva o SDK ou os fontes recuperados.

## Configuração inicial

1. Use um runner **Windows x64 dedicado** com label `electroinventory-release`. Instale Git, PowerShell 5.1, .NET SDK 8, Developer Pack .NET Framework 4.8 e Visual Studio Build Tools com MSBuild e ferramentas desktop .NET. Garanta acesso a GitHub e NuGet. Restrinja o runner a fluxos confiáveis; o build não deve aceitar PRs ou código não revisado. Restrinja a criação de tags `v*` e proteja `main` com rulesets.
2. Use uma instalação Legacy autorizada. Coloque a cópia oficial do SDK em `<GTA V>/SDK/RagePluginHookSDK.dll` e RAGENativeUI na raiz. A RC2 usa o NAudio versionado em seu projeto, com fallback para a instalação Legacy. Os pacotes .NET são os de `packages.config` da RC2. No ambiente **release-build**, configure `GTA_LEGACY_DIRECTORY` e `RPH_SDK_SHA256` (obtenha com `Get-FileHash -Algorithm SHA256` sobre o SDK que você verificou). Se os checkouts de dependências exigirem autenticação, configure `RELEASE_DEPENDENCIES_READ_TOKEN` com acesso somente leitura a ambos.
3. Configure os ambientes **release-build** e **release-publish** no GitHub, com revisores e restrições de branch/tag apropriados. A licença do LS RED restringe distribuir o binário, o código e versões modificadas. Somente depois de obter autorização do titular para este pacote, configure `LSR_REDISTRIBUTION_APPROVED=true` nos dois ambientes. O build pode ser validado sem upload de pacote quando a autorização não estiver configurada. Mantenha SDK, jogo e dependências de terceiros fora dos artefatos. [Licença de referência](https://github.com/jholleboy/lossantosred-ptbr/blob/ba4115b/README.md#license)

## Disparar e conferir

Em Actions → ElectroInventory Legacy release, execute manualmente sobre **main** com **build_only** desmarcado, informando uma versão como `v4.0.0`. O workflow cria a tag somente após build, testes, validação de pacote e autorização. Enviar uma tag `v*` aprovada também dispara o fluxo. Use `v4.0.1`, `v4.1.0` etc. quando os respectivos commits estiverem prontos. Uma execução nunca sobrescreve uma release existente. Se restar um rascunho após falha de publicação, o mantenedor deve verificar o rascunho e removê-lo ou completá-lo manualmente antes de repetir com a mesma tag.

O script restaura apenas os 30 fontes ausentes do commit oficial fixado e aplica os patches HUD e Inventory, nessa ordem. O ZIP usa uma lista de arquivos permitidos. O workflow confere a arquitetura e os hashes de build, reabre o ZIP, valida o hash de cada entrada e publica os três assets em uma prerelease. A DLL do ElectroHud fica fora do pacote. Promova a prerelease somente depois de confirmar os testes dentro do GTA V.

Se for preciso depurar o job localmente, use um checkout **descartável e limpo** da RC2. Defina a mesma informação de SDK e execute `release/BUILD_FULL.ps1` com os caminhos de RC2 e ElectroHud e a versão desejada. O script restaura e aplica patches diretamente nessa cópia de trabalho.

### Primeira validação privada

Em **Actions → ElectroInventory Legacy release → Run workflow**, escolha **main** e mantenha **build_only** marcado (padrão). A versão serve apenas para identificar esse build local. O runner executa a mesma compilação, os testes e a verificação das DLLs, do catálogo e do ZIP. O resumo mostra o resultado; abra o log do build para ver cada etapa ou a etapa da falha.

O modo privado requer o runner dedicado `electroinventory-release`, o SDK oficial e as variáveis de build documentadas acima. Ele dispensa `LSR_REDISTRIBUTION_APPROVED` e `release-publish`. O ZIP fica no runner, sem upload, tag ou release. Para a publicação autorizada, desmarque **build_only** ao disparar manualmente ou envie a tag aprovada.

### Regressão do empacotador no Windows PowerShell 5.1

O build executa `PACKAGE_FULL.ps1` sobre as saídas reais em um novo `powershell.exe -NoProfile -NonInteractive` e exige a versão 5.1. O teste é o próprio empacotamento completo: confere DLLs x64, catálogo, SHA-256 e o conteúdo reaberto do ZIP. Um erro no processo filho interrompe o build antes de qualquer upload. No .NET Framework, os tipos de ZIP exigem `System.IO.Compression` e `System.IO.Compression.FileSystem`; os demais tipos usados vêm das bibliotecas base ou dos recursos nativos do PowerShell.

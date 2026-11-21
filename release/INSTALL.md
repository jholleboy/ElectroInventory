# Instalação do ElectroInventory Legacy

O ZIP é uma atualização integrada para **GTA V Legacy** com RAGE Plugin Hook, Los Santos RED e suas dependências de execução já instaladas. Mantenha RAGENativeUI, NAudio e as outras dependências de execução da instalação oficial. O pacote contém o patch ElectroHud V6 compilado dentro da RC2. Para exibir a HUD, instale separadamente o ElectroHud V6 compatível.

1. Baixe o ZIP e o arquivo .sha256 da mesma prerelease. No PowerShell, confira o hash antes de extrair:

   ```powershell
   $zip = 'ElectroInventory-V4-Legacy-Full.zip'
   $expected = ((Get-Content "$zip.sha256" -Raw).Trim() -split '\s+')[0]
   if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $expected) { throw 'ZIP com hash incorreto.' }
   ```

2. Feche o GTA V e o RAGE Plugin Hook. Faça backup de `Plugins/ElectroInventory.dll`, `Plugins/Los Santos RED.dll` e `Plugins/LosSantosRED/Localization/LanguageStrings_pt-BR.xml`, se existirem. Guarde também o manifesto anterior e seu Settings.xml. Anote quais arquivos ainda não existem.
3. Extraia **na raiz do GTA V Legacy**, onde fica GTA5.exe. Permita substituir as duas DLLs, o catálogo e o manifesto. O ZIP inclui `Plugins/ElectroInventory/Settings.default.xml` como exemplo. O plugin usa padrões internos quando `Settings.xml` está ausente, e a extração preserva configurações personalizadas.
4. Confira a versão no arquivo `ElectroInventory-release.json` da raiz. Inicie em modo história, com um save de teste. [Roteiro de validação](https://github.com/jholleboy/ElectroInventory/blob/main/TESTING.md)

## Rollback

Feche o jogo. Restaure as DLLs e o catálogo do mesmo backup, recoloque o manifesto anterior e remova apenas os arquivos listados acima que foram introduzidos pela atualização. Preserve Settings.xml e seus saves. Se trocar de volta para a RC2 sem as pontes, use as versões correspondentes dos plugins ou descarregue os plugins de integração.

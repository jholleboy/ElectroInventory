#requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$InventoryCommit,
    [Parameter(Mandatory)][string]$Rc2Commit,
    [Parameter(Mandatory)][string]$HudCommit,
    [Parameter(Mandatory)][string]$UpstreamCommit,
    [Parameter(Mandatory)][string]$InventoryRoot,
    [Parameter(Mandatory)][string]$Rc2Build
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Hash-File([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

function Assert-X64Assembly([string]$Path, [string]$Name) {
    if ([Reflection.AssemblyName]::GetAssemblyName($Path).Name -ne $Name) { throw "Assembly invalida: $Name" }
    $stream = [IO.File]::OpenRead($Path)
    try {
        $reader = [IO.BinaryReader]::new($stream)
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw "Cabecalho DOS invalido: $Name" }
        $stream.Position = 0x3c
        $stream.Position = $reader.ReadInt32()
        if ($reader.ReadUInt32() -ne 0x00004550 -or $reader.ReadUInt16() -ne 0x8664) { throw "Saida nao x64: $Name" }
    }
    finally { $stream.Dispose() }
}

$inventoryBuild = Join-Path $InventoryRoot 'bin\Release'
$inventoryDll = Join-Path $inventoryBuild 'ElectroInventory.dll'
$rc2Dll = Join-Path $Rc2Build 'Los Santos RED.dll'
$catalog = Join-Path $Rc2Build 'LosSantosRED\Localization\LanguageStrings_pt-BR.xml'
Assert-X64Assembly $inventoryDll 'ElectroInventory'
Assert-X64Assembly $rc2Dll 'Los Santos RED'
$rc2Manifest = Get-Content -LiteralPath (Join-Path $Rc2Build 'build.json') -Raw | ConvertFrom-Json
if ($rc2Manifest.edition -ne 'Legacy' -or (Hash-File $rc2Dll) -ne $rc2Manifest.dllSha256 -or
    (Hash-File $catalog) -ne $rc2Manifest.catalogSha256 -or
    (Hash-File $inventoryDll) -ne (Get-Content -LiteralPath (Join-Path $inventoryBuild 'build.sha256') -Raw).Trim()) {
    throw 'A saida difere dos hashes de compilacao.'
}
[xml]$catalogXml = Get-Content -LiteralPath $catalog -Raw
$entries = @($catalogXml.SelectNodes("//*[local-name()='LanguageString']"))
if ($entries.Count -eq 0) { throw 'Catalogo PT-BR vazio.' }
$keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($entry in $entries) {
    if ([string]::IsNullOrWhiteSpace($entry.ID) -or -not $keys.Add([string]$entry.ID)) { throw 'Chave PT-BR ausente ou duplicada.' }
}
$defaults = Join-Path $InventoryRoot 'Settings.xml'
[xml]$settings = Get-Content -LiteralPath $defaults -Raw
if ($settings.DocumentElement.LocalName -ne 'ElectroInventory') { throw 'Configuracao invalida.' }

$output = Join-Path $InventoryRoot 'artifacts\release'
if (Test-Path -LiteralPath $output) { throw 'Diretorio de release ja existe; use um checkout limpo.' }
$temp = Join-Path ([IO.Path]::GetTempPath()) ('ei-package-' + [guid]::NewGuid())
$stage = Join-Path $temp 'stage'
try {
    $files = [ordered]@{
        'Plugins/ElectroInventory.dll' = $inventoryDll
        'Plugins/Los Santos RED.dll' = $rc2Dll
        'Plugins/ElectroInventory/Settings.default.xml' = $defaults
        'Plugins/LosSantosRED/Localization/LanguageStrings_pt-BR.xml' = $catalog
        'INSTALL.md' = (Join-Path $PSScriptRoot 'INSTALL.md')
    }
    $hashes = [ordered]@{}
    foreach ($relative in $files.Keys) {
        $destination = Join-Path $stage $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $files[$relative] -Destination $destination
        $hashes[$relative] = Hash-File $destination
    }
    $manifest = [ordered]@{
        version = $Version
        electroInventoryCommit = $InventoryCommit
        rc2Commit = $Rc2Commit
        electroHudPatchCommit = $HudCommit
        upstreamSourcesCommit = $UpstreamCommit
        configuration = 'Release x64'
        gtaTested = $false
        files = $hashes
    }
    $manifestJson = $manifest | ConvertTo-Json -Depth 4
    $manifestJson | Set-Content -LiteralPath (Join-Path $stage 'ElectroInventory-release.json') -Encoding ASCII
    $hashes['ElectroInventory-release.json'] = Hash-File (Join-Path $stage 'ElectroInventory-release.json')

    $name = 'ElectroInventory-V4-Legacy-Full.zip'
    $zip = Join-Path $temp $name
    $writer = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($relative in $hashes.Keys) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($writer, (Join-Path $stage $relative), $relative) | Out-Null
        }
    }
    finally { $writer.Dispose() }
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        if ($archive.Entries.Count -ne $hashes.Count) { throw 'Quantidade de entradas inesperada no ZIP.' }
        foreach ($relative in $hashes.Keys) {
            $entry = $archive.GetEntry($relative)
            if ($null -eq $entry -or $entry.Length -le 0) { throw "Entrada invalida no ZIP: $relative" }
            $stream = $entry.Open()
            try {
                if ((Get-FileHash -InputStream $stream -Algorithm SHA256).Hash -ne $hashes[$relative]) {
                    throw "Hash incorreto no ZIP: $relative"
                }
            }
            finally { $stream.Dispose() }
        }
    }
    finally { $archive.Dispose() }

    $zipHash = Hash-File $zip
    New-Item -ItemType Directory -Path $output | Out-Null
    Move-Item -LiteralPath $zip -Destination $output
    [IO.File]::WriteAllText((Join-Path $output "$name.sha256"), "$zipHash  $name" + [char]10, [Text.Encoding]::ASCII)
    $manifestJson | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding ASCII
    @"
# ElectroInventory $Version — candidato Legacy

- ElectroInventory: $InventoryCommit
- Los Santos RED PT-BR RC2: $Rc2Commit
- Patch ElectroHud V6: $HudCommit
- Fontes ausentes recuperadas do upstream: $UpstreamCommit
- SHA-256 do ZIP: $zipHash

Inclui o ElectroInventory, a DLL RC2 com as duas pontes, o catálogo PT-BR e uma configuração de exemplo. [Alterações e limitações](https://github.com/jholleboy/ElectroInventory/blob/$InventoryCommit/INTEGRATION.md)

Feche o GTA V, faça backup dos arquivos listados e extraia na raiz do GTA V Legacy. ElectroHud.dll e dependências do jogo são externos ao ZIP. [Instalação e rollback](https://github.com/jholleboy/ElectroInventory/blob/$InventoryCommit/release/INSTALL.md)

Esta candidata ainda precisa de teste dentro do GTA V.
"@ | Set-Content -LiteralPath (Join-Path $output 'RELEASE_NOTES.md') -Encoding UTF8
    Write-Host "ZIP validado: $zipHash"
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

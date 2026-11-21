#requires -Version 5.1
param(
    [Parameter(Mandatory)][ValidatePattern('^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')][string]$Version,
    [Parameter(Mandatory)][string]$GtaVDirectory,
    [Parameter(Mandatory)][string]$Rc2Directory,
    [Parameter(Mandatory)][string]$HudDirectory
)
$ErrorActionPreference = 'Stop'
$inventory = Split-Path -Parent $PSScriptRoot
$rc2Sha = 'ba4115b7c6b5dc0986c19a88fc121a9c9ea33af5'
$hudSha = 'c9ba71350ec3b4b358453c5a1bfcdabc82eb21fe'
$upstreamSha = '9ccb4e85a8b6a9a0ff172acc4b6ab46c38605a4e'

function Invoke-Git {
    & git @args
    if ($LASTEXITCODE -ne 0) { throw "Git falhou ($LASTEXITCODE)." }
}
$stage = 'Verificar checkouts e SDK'
$temp = $null
try {
    Write-Host "[BUILD] $stage"
    $rc2 = (Resolve-Path -LiteralPath $Rc2Directory).ProviderPath
    $hud = (Resolve-Path -LiteralPath $HudDirectory).ProviderPath
    $game = (Resolve-Path -LiteralPath $GtaVDirectory).ProviderPath
    $inventorySha = (Invoke-Git -C $inventory rev-parse HEAD).Trim()
    if ((Invoke-Git -C $rc2 rev-parse HEAD).Trim() -ne $rc2Sha -or
        (Invoke-Git -C $hud rev-parse HEAD).Trim() -ne $hudSha) {
        throw 'Checkouts RC2 ou ElectroHud diferentes das referencias fixadas.'
    }
    if (Invoke-Git -C $rc2 status --porcelain) { throw 'Use um checkout limpo da RC2.' }
    foreach ($relative in @('GTA5.exe', 'SDK\RagePluginHookSDK.dll', 'RAGENativeUI.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $game $relative) -PathType Leaf)) { throw "Dependencia Legacy ausente: $relative" }
    }
    if ($env:RPH_SDK_SHA256 -notmatch '^[a-fA-F0-9]{64}$' -or
        (Get-FileHash -LiteralPath (Join-Path $game 'SDK\RagePluginHookSDK.dll') -Algorithm SHA256).Hash -ne $env:RPH_SDK_SHA256) {
        throw 'Configure RPH_SDK_SHA256 com o hash da copia oficial aprovada no runner.'
    }

    $temp = Join-Path ([IO.Path]::GetTempPath()) ('ei-original-' + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $temp | Out-Null
    $stage = 'Identificar e restaurar os 30 fontes originais'
    Write-Host "[BUILD] $stage"
    $original = Join-Path $temp 'source'
    $project = Join-Path $rc2 'Los Santos RED\Los Santos RED.csproj'
    [xml]$xml = Get-Content -LiteralPath $project -Raw
    $missing = @(
        foreach ($entry in $xml.SelectNodes("//*[local-name()='Compile']")) {
            $relative = 'Los Santos RED/' + $entry.Include.Replace('\', '/')
            if (-not (Test-Path -LiteralPath (Join-Path $rc2 $relative) -PathType Leaf)) {
                if ($relative -notmatch '^Los Santos RED/[^:*?]+\.cs$' -or $relative -match '(^|/)\.\.(/|$)') {
                    throw "Fonte insegura ou nao C#: $relative"
                }
                $relative
            }
        }
    ) | Sort-Object -Unique
    if (@($missing).Count -ne 30) { throw "Esperava 30 fontes ausentes no commit RC2, encontrei $(@($missing).Count)." }
    Invoke-Git init --quiet $original
    Invoke-Git -C $original fetch --depth 1 https://github.com/thatoneguy650/Los-Santos-RED.git $upstreamSha
    if ((Invoke-Git -C $original rev-parse FETCH_HEAD).Trim() -ne $upstreamSha) { throw 'Upstream diferente do commit fixado.' }
    $archive = Join-Path $temp 'sources.tar'
    Invoke-Git -C $original archive --format=tar "--output=$archive" $upstreamSha -- @missing
    & tar.exe -xf $archive -C $rc2
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao extrair fontes originais.' }
    foreach ($file in $missing) {
        if (-not (Test-Path -LiteralPath (Join-Path $rc2 $file) -PathType Leaf)) { throw "Fonte ausente: $file" }
    }

    $stage = 'Verificar e aplicar patches RC2'
    Write-Host "[BUILD] $stage"
    foreach ($patch in @(
        (Join-Path $hud 'integration\LosSantosRED-ba4115b-v6.patch'),
        (Join-Path $inventory 'integration\LosSantosRED-ba4115b-electroinventory-actions-v2.patch')
    )) {
        Write-Host "[BUILD] Patch: $(Split-Path -Leaf $patch)"
        Invoke-Git -C $rc2 apply --check $patch
        Invoke-Git -C $rc2 apply $patch
    }
    Invoke-Git -C $rc2 diff --check

    $stage = 'Compilar RC2 integrada (Release x64)'
    Write-Host "[BUILD] $stage"
    & (Join-Path $rc2 'BUILD_PTBR.ps1') -GtaVDirectory $game -Edition Legacy
    $stage = 'Compilar ElectroInventory (Release x64)'
    Write-Host "[BUILD] $stage"
    & (Join-Path $inventory 'BUILD.ps1') -GtaVDirectory $game
    $stage = 'Executar testes automatizados'
    Write-Host "[BUILD] $stage"
    & dotnet run --project (Join-Path $inventory 'tests\ProtocolTests.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Testes automatizados falharam.' }

    $stage = 'Validar DLLs, catalogo PT-BR e ZIP em processo limpo do Windows PowerShell 5.1'
    Write-Host "[BUILD] $stage"
    $windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $packageArgs = @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-File', (Join-Path $PSScriptRoot 'PACKAGE_FULL.ps1')
        '-Version', $Version, '-InventoryCommit', $inventorySha, '-Rc2Commit', $rc2Sha
        '-HudCommit', $hudSha, '-UpstreamCommit', $upstreamSha, '-InventoryRoot', $inventory
        '-Rc2Build', (Join-Path $rc2 'artifacts\ptbr\build\Legacy'), '-RequireWindowsPowerShell51'
    )
    # Run the production packager as the regression test on the actual Release x64 outputs.
    & $windowsPowerShell @packageArgs
    if ($LASTEXITCODE -ne 0) { throw "Empacotamento isolado falhou ($LASTEXITCODE)." }
    Write-Host '[BUILD] PASSOU: Release x64, testes e verificacao do ZIP local.'
}
catch {
    throw "[BUILD] FALHOU na etapa '$stage': $($_.Exception.Message)"
}
finally {
    if ($temp -and (Test-Path -LiteralPath $temp)) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

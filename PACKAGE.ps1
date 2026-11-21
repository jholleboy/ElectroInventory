#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$build = Join-Path $PSScriptRoot 'bin\Release'
$dll = Join-Path $build 'ElectroInventory.dll'
$stamp = Join-Path $build 'build.sha256'
foreach ($file in @($dll, $stamp)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw 'Execute BUILD.ps1 antes de empacotar.' }
}
if ((Get-Content -LiteralPath $stamp -Raw).Trim() -ne (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash) {
    throw 'A DLL mudou após o build. Execute BUILD.ps1 novamente.'
}

$artifacts = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$zip = Join-Path $artifacts 'ElectroInventory-Legacy.zip'
if (Test-Path -LiteralPath $zip) { throw "Mova o pacote anterior antes de empacotar: $zip" }

$temp = Join-Path $artifacts ('stage-' + [guid]::NewGuid().ToString('N'))
$archive = "$temp.zip"
try {
    $plugins = Join-Path $temp 'Plugins'
    $mod = Join-Path $plugins 'ElectroInventory'
    New-Item -ItemType Directory -Path $mod -Force | Out-Null
    Copy-Item -LiteralPath $dll -Destination $plugins
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Settings.xml') -Destination $mod
    foreach ($doc in @('README.md', 'INTEGRATION.md', 'TESTING.md')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $doc) -Destination $temp
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'integration') -Destination $temp -Recurse
    Compress-Archive -Path (Join-Path $temp '*') -DestinationPath $archive
    Move-Item -LiteralPath $archive -Destination $zip
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
}
Write-Host $zip

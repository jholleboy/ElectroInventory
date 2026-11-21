#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$GtaVDirectory)

$ErrorActionPreference = 'Stop'
$gta = (Resolve-Path -LiteralPath $GtaVDirectory).ProviderPath
$build = Join-Path $PSScriptRoot 'bin\Release'
$dll = Join-Path $build 'ElectroInventory.dll'
$stamp = Join-Path $build 'build.sha256'
if (Test-Path -LiteralPath $stamp) { Remove-Item -LiteralPath $stamp }

foreach ($relative in @('GTA5.exe', 'SDK\RagePluginHookSDK.dll')) {
    $file = Join-Path $gta $relative
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Arquivo ausente: $file" }
}

$programFiles = [Environment]::GetFolderPath('ProgramFilesX86')
$framework = Join-Path $programFiles 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
foreach ($relative in @('mscorlib.dll', 'RedistList\FrameworkList.xml')) {
    if (-not (Test-Path -LiteralPath (Join-Path $framework $relative) -PathType Leaf)) {
        throw 'Instale o Developer Pack do .NET Framework 4.8.'
    }
}

$command = Get-Command MSBuild.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
$msbuild = if ($command) { $command.Source }
if (-not $msbuild) {
    $vswhere = Join-Path $programFiles 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere -PathType Leaf) {
        $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\Current\Bin\MSBuild.exe' | Select-Object -First 1
    }
}
if (-not $msbuild) { throw 'MSBuild ausente. Instale o Visual Studio Build Tools com desenvolvimento desktop .NET.' }

& $msbuild (Join-Path $PSScriptRoot 'ElectroInventory.sln') /nologo /t:Rebuild /p:Configuration=Release /p:Platform=x64 "/p:GtaVDirectory=$gta"
if ($LASTEXITCODE -ne 0) { throw "Falha do MSBuild ($LASTEXITCODE)." }
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "DLL ausente após o build: $dll" }

(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash | Set-Content -LiteralPath $stamp -Encoding ASCII
Write-Host "Build concluído: $dll"

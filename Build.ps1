param([Parameter(Mandatory=$true)][string]$SptPath)
$ErrorActionPreference = 'Stop'
$sdk = (& dotnet --list-sdks | Select-Object -Last 1)
if ($sdk -notmatch '^([^ ]+) \[(.+)\]') { throw 'Kein .NET SDK gefunden.' }
$compiler = Join-Path $Matches[2] ($Matches[1] + '\Roslyn\bincore\csc.dll')
$dotnetRoot = Split-Path $Matches[2]
$refRoot = Join-Path $dotnetRoot 'packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1'
if (!(Test-Path $refRoot)) { throw 'NETStandard.Library.Ref 2.1.0 fehlt.' }
$managed = Join-Path $SptPath 'EscapeFromTarkov_Data\Managed'
$references = @(Get-ChildItem $refRoot -Filter *.dll | ForEach-Object FullName)
$references += @('Assembly-CSharp','Comfort','Comfort.Unity','UnityEngine','UnityEngine.CoreModule','UnityEngine.PhysicsModule','UnityEngine.InputLegacyModule','UnityEngine.IMGUIModule') | ForEach-Object { Join-Path $managed ($_ + '.dll') }
$references += @('BepInEx.dll','0Harmony.dll') | ForEach-Object { Join-Path $SptPath ('BepInEx\core\' + $_) }
foreach ($reference in $references) { if (!(Test-Path -LiteralPath $reference)) { throw "Datei fehlt: $reference" } }
$result = Join-Path $PSScriptRoot 'RescueNoClip.dll'
$response = Join-Path $PSScriptRoot 'build.rsp'
@('/nostdlib+','/target:library','/optimize+',('/out:"' + $result + '"')) + @($references | ForEach-Object { '/r:"' + $_ + '"' }) + @('"' + (Join-Path $PSScriptRoot 'RescueNoClip.cs') + '"') | Set-Content -LiteralPath $response
& dotnet $compiler ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw 'Kompilierung fehlgeschlagen.' }
Write-Output "Erstellt: $result"

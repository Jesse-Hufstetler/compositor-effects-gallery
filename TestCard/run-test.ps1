# Shows the striped test card with the gallery on top for a few seconds per scroll position and saves pictures
# of the card's rectangle to test-output. Usage: .\run-test.ps1 0 1100   (add -Activate to give the gallery focus)
param([int[]]$Scroll = @(0), [int]$Width = 2400, [int]$Height = 1300, [switch]$Activate, [string]$Extra = '', [string]$Tag = '', [switch]$Bars)

$root = Split-Path $PSScriptRoot
$gallery = Join-Path $root 'bin\Release\net10.0-windows10.0.22621.0\CompositorEffectsGallery.exe'
$card = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows\TestCard.exe'
$out = Join-Path $root 'test-output'
$crash = Join-Path (Split-Path $gallery) 'crash.txt'

$cardArgs = @("`"$gallery`"", "`"$out`"", $Width, $Height) + $Scroll
if ($Activate) { $cardArgs += '--activate' }
if ($Extra) { $cardArgs += '--extra', "`"$Extra`"" }
if ($Tag) { $cardArgs += '--tag', $Tag }
if ($Bars) { $cardArgs += '--bars' }
$p = Start-Process $card -ArgumentList $cardArgs -PassThru -Wait
"exit=$($p.ExitCode)"
if (Test-Path $crash) { Get-Content $crash | Select-Object -First 30 }
if (Test-Path "$out\results.txt") { Get-Content "$out\results.txt" }

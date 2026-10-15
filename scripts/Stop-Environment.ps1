param([ValidateSet('dev','production')][string]$Flavor='dev')
& (Join-Path $PSScriptRoot 'Start-Environment.ps1') -Flavor $Flavor -Action stop

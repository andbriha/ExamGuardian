param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Options)
$ErrorActionPreference='Stop'
$allowed=@('--production','--prod','--check','--stop','--status')
foreach ($option in $Options) { if ($option -notin $allowed) { throw "Unknown argument: $option" } }
$actions=@($Options | Where-Object { $_ -in @('--check','--stop','--status') })
if ($actions.Count -gt 1) { throw 'Choose one action: --check, --stop or --status.' }
$flavor=if ($Options -contains '--production' -or $Options -contains '--prod') {'production'} else {'dev'}
& (Join-Path $PSScriptRoot 'scripts/Start-Environment.ps1') -Flavor $flavor -Action $(if($actions.Count){$actions[0].Substring(2)}else{'start'})
if ($LASTEXITCODE) { exit $LASTEXITCODE }

param([ValidateSet('dev','production')][string]$Flavor='dev', [ValidateSet('start','check','stop','status')][string]$Action='start')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$config=Join-Path $root ".env.$Flavor"
function New-EnvironmentSecret {
    $bytes=New-Object byte[] 48
    $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
    try {$rng.GetBytes($bytes)} finally {$rng.Dispose()}
    [Convert]::ToBase64String($bytes)
}
if (!(Test-Path -LiteralPath $config)) {
    if ($Flavor -eq 'production' -or $Action -in @('stop','status')) { throw "Missing .env.$Flavor. Configure the selected environment explicitly." }
    $values=[IO.File]::ReadAllText((Join-Path $root '.env.dev.example'))
    foreach ($key in @('DB_PASSWORD','JWT__KEY','AiService__ApiKey','BOOTSTRAP_EXAMINER_PASSWORD')) {
        $values=[regex]::Replace($values,"(?m)^$key=.*$", "$key=$(New-EnvironmentSecret)")
    }
    # Existing documentation servers are preserved. Choose an available dev docs port on first initialization.
    $docsPort=4321
    if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
        $used=@(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty LocalPort)
        while ($docsPort -in $used -and $docsPort -lt 4331) {$docsPort++}
        if ($docsPort -in $used) {throw 'No free documentation port in 4321–4331.'}
    }
    $values=$values.Replace('DOCS_PORT=4321',"DOCS_PORT=$docsPort")
    [IO.File]::WriteAllText($config,$values,(New-Object Text.UTF8Encoding $false))
    Write-Host 'Created .env.dev with independent random secrets. Examiner credentials remain only in that file.'
}
$settings=@{}
foreach ($line in [IO.File]::ReadAllLines($config)) {
    if ($line -match '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
        if ($settings.ContainsKey($matches[1])) {throw "Duplicate configuration field: $($matches[1])"}
        $settings[$matches[1]]=$matches[2].Trim()
    }
}
if ($Action -in @('start','check')) {
    $required=@('DB_NAME','DB_USER','DB_PASSWORD','JWT__KEY','JWT__ISSUER','JWT__AUDIENCE','AiService__ApiKey','BOOTSTRAP_EXAMINER_LOGIN','BOOTSTRAP_EXAMINER_PASSWORD')
    foreach ($key in $required) {if (!$settings[$key] -or $settings[$key] -match 'REPLACE_|CHANGEME') {throw "Configure $key in .env.$Flavor."}}
    if ($settings['JWT__KEY'].Length -lt 32) {throw 'JWT__KEY must contain at least 32 characters.'}
    if ($Flavor -eq 'production') {
        foreach ($key in @('TLS_DIRECTORY','PUBLIC_ORIGIN')) {if (!$settings[$key]) {throw "Configure $key in .env.production."}}
        $origin=$null
        if (![Uri]::TryCreate($settings['PUBLIC_ORIGIN'],[UriKind]::Absolute,[ref]$origin) -or $origin.Scheme -ne 'https' -or $origin.Host -eq 'exam.example.org') {throw 'Configure the actual HTTPS PUBLIC_ORIGIN.'}
        foreach ($file in @('fullchain.pem','privkey.pem')) {if (!(Test-Path -LiteralPath (Join-Path $settings['TLS_DIRECTORY'] $file))) {throw "TLS certificate file missing: $file"}}
    }
}
$compose=@('compose','--project-directory',$root,'--project-name',"examguardian-$Flavor",'--env-file',$config,'-f',(Join-Path $root 'compose.base.yml'),'-f',(Join-Path $root "compose.$Flavor.yml"))
& docker @compose config --quiet
if ($LASTEXITCODE -ne 0) {throw 'Selected Compose configuration is invalid.'}
Write-Host "Environment: $Flavor | Project: examguardian-$Flavor"
if ($Action -eq 'check') {Write-Host 'Configuration valid. No containers changed.';return}
if ($Action -eq 'stop') {& docker @compose stop; if ($LASTEXITCODE -ne 0) {throw 'Stop failed.'};return}
if ($Action -eq 'status') {& docker @compose ps;return}
if ($Flavor -eq 'dev') {
    & docker @compose up -d --build db docs
    if ($LASTEXITCODE -ne 0) {throw 'Development database/documentation startup failed.'}
    Write-Host "Documentation: http://127.0.0.1:$($settings['DOCS_PORT'])/docs/09-deployment-view"
}
& docker @compose up -d --build --wait --wait-timeout 300
if ($LASTEXITCODE -ne 0) {& docker @compose ps;throw 'Stack did not become ready. Inspect service logs; no fallback environment was started.'}
$url=if($Flavor -eq 'dev'){"http://127.0.0.1:$($settings['APP_PORT'])"}else{$settings['PUBLIC_ORIGIN']}
Write-Host "Application: $url | Examiner panel: $url/login"
Write-Host 'Student client runs natively. Use the matching environment build and origin.'
& docker @compose ps

param([string]$Workspace = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$configPath = Join-Path $Workspace '.env'
if (Test-Path -LiteralPath $configPath) { Write-Output '.env already exists; existing credentials were preserved.'; exit 0 }
function New-Secret([int]$Length = 32) {
    $bytes = New-Object byte[] $Length
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($bytes) } finally { $random.Dispose() }
    [Convert]::ToBase64String($bytes)
}
$configuration = @"
DB_NAME=examguardian_db
DB_USER=postgres
DB_PASSWORD=$(New-Secret)
ASPNETCORE_ENVIRONMENT=Development
JWT__KEY=$(New-Secret 48)
JWT__ISSUER=ExamGuardianServer
JWT__AUDIENCE=ExamGuardianClient
JWT__LIFETIME_MINUTES=1440
AiService__ApiKey=$(New-Secret 48)
BOOTSTRAP_EXAMINER_LOGIN=examiner
BOOTSTRAP_EXAMINER_PASSWORD=$(New-Secret)
SEED_DEMO_EXAM=false
NGINX_PORT=80
BACKEND_URL=/api
"@
[System.IO.File]::WriteAllText($configPath, $configuration, (New-Object System.Text.UTF8Encoding $false))
Write-Output 'Created .env with random secrets. First examiner credentials are stored only in this file.'

param([switch]$DevelopmentTools, [switch]$KeepEnvironment, [string]$PythonExecutable="python")
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$suffix=[Guid]::NewGuid().ToString('N').Substring(0,10)
$network="examguardian-contract-$suffix"
$db="examguardian-contract-db-$suffix"
$api="examguardian-contract-api-$suffix"
$media=Join-Path ([IO.Path]::GetTempPath()) "examguardian-contract-$suffix"
New-Item -ItemType Directory -Path $media | Out-Null
function Check-Exit([string]$operation) { if ($LASTEXITCODE -ne 0) { throw "$operation failed." } }
try {
    $devFlag=if($DevelopmentTools){'true'}else{'false'}
    docker build --build-arg "EnableDevelopmentTools=$devFlag" -t examguardian-core-contract:local (Join-Path $root 'Core'); Check-Exit 'API build'
    docker network create $network | Out-Null; Check-Exit 'Test network'
    docker run -d --name $db --network $network --network-alias db --tmpfs /var/lib/postgresql/data -e POSTGRES_USER=tester -e POSTGRES_PASSWORD=examguardian-test-only -e POSTGRES_DB=examguardian_test postgres:16-alpine | Out-Null; Check-Exit 'Test database'
    $ready=$false
    for ($i=0;$i -lt 30;$i++) { docker exec $db pg_isready -U tester -d examguardian_test *> $null; if ($LASTEXITCODE -eq 0) {$ready=$true;break}; Start-Sleep -Seconds 1 }
    if (!$ready) {throw 'Test database not ready.'}
    docker run -d --name $api --network $network -p 127.0.0.1:55111:8080 -e 'ConnectionStrings__DefaultConnection=Host=db;Database=examguardian_test;Username=tester;Password=examguardian-test-only' -e Jwt__Key=examguardian-integration-test-signing-key-2026 -e Jwt__Issuer=ExamGuardianTest -e Jwt__Audience=ExamGuardianTestClient -e AiService__ApiKey=examguardian-integration-test-ai-key -e Bootstrap__ExaminerLogin=admin -e Bootstrap__ExaminerPassword=Contract-Bootstrap-Password-2026! -e "Development__AllowIdentityBypass=$devFlag" -e ASPNETCORE_ENVIRONMENT=Development -e Logging__LogLevel__Default=Warning examguardian-core-contract:local | Out-Null; Check-Exit 'Test API'
    $ready=$false
    for ($i=0;$i -lt 45;$i++) { try { $response=Invoke-WebRequest http://127.0.0.1:55111/health/ready -UseBasicParsing -TimeoutSec 1; if ($response.StatusCode -eq 200) {$ready=$true;break} } catch {}; Start-Sleep -Seconds 1 }
    if (!$ready) {docker logs $api; throw 'Test API not ready.'}
    docker run --rm --entrypoint ffmpeg --mount "type=bind,source=$media,target=/test" examguardian-core-contract:local -loglevel error -f lavfi -i color=c=gray:s=160x120:d=1 -an -c:v libx264 -pix_fmt yuv420p -y /test/contract.mp4; Check-Exit 'Test video generation'
    $env:EXAMGUARDIAN_TEST_DEVELOPMENT=$devFlag
    $env:EXAMGUARDIAN_TEST_URL='http://127.0.0.1:55111'
    $env:EXAMGUARDIAN_TEST_KEY='examguardian-integration-test-signing-key-2026'
    $env:EXAMGUARDIAN_TEST_PASSWORD='Contract-Bootstrap-Password-2026!'
    $env:EXAMGUARDIAN_TEST_DB_CONTAINER=$db
    $env:EXAMGUARDIAN_TEST_API_CONTAINER=$api
    $env:EXAMGUARDIAN_TEST_VIDEO=Join-Path $media 'contract.mp4'
    & $PythonExecutable (Join-Path $PSScriptRoot 'test_core_contract.py'); Check-Exit 'Core contracts'
    & $PythonExecutable (Join-Path $PSScriptRoot 'test_retention_contract.py'); Check-Exit 'Retention contract'
} finally {
    if (!$KeepEnvironment) { docker rm --force $api $db 2>$null | Out-Null; docker network rm $network 2>$null | Out-Null }
}

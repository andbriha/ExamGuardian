param([Parameter(Mandatory)][string]$Destination, [string]$Database = 'examguardian_db', [string]$DatabaseUser = 'postgres')
$ErrorActionPreference = 'Stop'
if ($Database -notmatch '^[a-zA-Z0-9_]+$' -or $DatabaseUser -notmatch '^[a-zA-Z0-9_]+$') { throw 'Invalid database identifier.' }
$target = [System.IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $target) { throw 'Choose a new empty backup directory.' }
New-Item -ItemType Directory -Path $target | Out-Null
$archive = '/tmp/examguardian-' + [Guid]::NewGuid().ToString('N') + '.dump'
docker compose exec -T db pg_dump -U $DatabaseUser -d $Database -Fc -f $archive
if ($LASTEXITCODE -ne 0) { throw 'Database backup failed.' }
docker compose cp "db:$archive" (Join-Path $target 'database.dump')
if ($LASTEXITCODE -ne 0) { throw 'Database archive copy failed.' }
docker compose cp 'api:/app/ExamVideos' (Join-Path $target 'videos')
if ($LASTEXITCODE -ne 0) { throw 'Video backup failed.' }
docker compose cp 'api:/app/InferenceImages' (Join-Path $target 'images')
if ($LASTEXITCODE -ne 0) { throw 'Image backup failed.' }
$stateArchive = '/tmp/backup-' + [Guid]::NewGuid().ToString('N')
$backupCode = 'import sqlite3,pathlib,sys; p=pathlib.Path(sys.argv[1]); p.mkdir(); files=list(pathlib.Path("/app/state").glob("*.sqlite3")); [(sqlite3.connect(str(f)).backup(sqlite3.connect(str(p/f.name)))) for f in files]'
docker compose exec -T ai_service python -c $backupCode $stateArchive
if ($LASTEXITCODE -ne 0) { throw 'Inference state backup failed.' }
docker compose cp "ai_service:$stateArchive" (Join-Path $target 'state')
if ($LASTEXITCODE -ne 0) { throw 'Inference state archive copy failed.' }
@{database=$Database; databaseUser=$DatabaseUser; createdUtc=[DateTime]::UtcNow.ToString('O'); format=1} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'backup.json') -Encoding utf8
Get-ChildItem -LiteralPath $target -Recurse -File | ForEach-Object {
    @{Path=$_.FullName.Substring($target.TrimEnd([IO.Path]::DirectorySeparatorChar).Length+1); Hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'checksums.json') -Encoding UTF8
Write-Output "Backup created: $target. For a coherent checkpoint, run after active attempts and outboxes have drained."

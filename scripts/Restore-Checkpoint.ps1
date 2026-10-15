param([Parameter(Mandatory)][string]$BackupDirectory, [Parameter(Mandatory)][string]$Destination, [int]$Port=55433)
$ErrorActionPreference = 'Stop'
$backup = [IO.Path]::GetFullPath($BackupDirectory)
$target = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $target) { throw 'Restore requires a new empty directory.' }
if ($Port -lt 1024 -or $Port -gt 65535) { throw 'Invalid local test port.' }
$checksums = Get-Content -LiteralPath (Join-Path $backup 'checksums.json') -Raw | ConvertFrom-Json
foreach ($entry in $checksums) {
    $file = [IO.Path]::GetFullPath((Join-Path $backup $entry.Path))
    if (!$file.StartsWith($backup + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Archive path escapes the backup directory.' }
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Hash) { throw "Backup checksum mismatch: $($entry.Path)" }
}
New-Item -ItemType Directory -Path $target | Out-Null
foreach ($folder in @('videos','images','state')) { Copy-Item -LiteralPath (Join-Path $backup $folder) -Destination (Join-Path $target $folder) -Recurse }
$name = 'examguardian-restore-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$bytes=New-Object byte[] 32
$random=[Security.Cryptography.RandomNumberGenerator]::Create()
try { $random.GetBytes($bytes) } finally { $random.Dispose() }
$secret=[Convert]::ToBase64String($bytes)
$environment = Join-Path $target '.restore.env'
"POSTGRES_PASSWORD=$secret" | Set-Content -LiteralPath $environment -Encoding ascii
docker run --detach --name $name --env-file $environment -e POSTGRES_USER=restore -e POSTGRES_DB=examguardian_restore -p "127.0.0.1:${Port}:5432" --mount "type=volume,source=${name}-data,target=/var/lib/postgresql/data" postgres:16-alpine
if ($LASTEXITCODE -ne 0) { throw 'Could not start isolated restore database.' }
$ready=$false
for ($i=0;$i -lt 30;$i++) {
    docker exec $name pg_isready -U restore -d examguardian_restore *> $null
    if ($LASTEXITCODE -eq 0) { $ready=$true; break }
    Start-Sleep -Seconds 1
}
if (!$ready) { throw 'Restore database did not become ready.' }
docker cp (Join-Path $backup 'database.dump') "${name}:/tmp/database.dump"
if ($LASTEXITCODE -ne 0) { throw 'Could not copy database archive.' }
docker exec $name pg_restore -U restore -d examguardian_restore --no-owner --no-privileges --exit-on-error /tmp/database.dump
if ($LASTEXITCODE -ne 0) { throw 'Restore failed. The isolated files and container are preserved for diagnosis.' }
@{container=$name; dataVolume="${name}-data"; port=$Port; database='examguardian_restore'; user='restore'; sourceBackup=$backup} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'restore.json') -Encoding utf8
Write-Output "Restored into isolated container $name on localhost:$Port. Media and inference state: $target. Original deployment is unchanged."

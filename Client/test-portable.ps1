param(
    [Parameter(Mandatory=$true)][string]$ArchivePath,
    [ValidateSet('development','production')][string]$Flavor='production',
    [string]$ExtractTo
)
$ErrorActionPreference='Stop'
$archive=[IO.Path]::GetFullPath($ArchivePath)
$temporaryRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$cleanup=!$ExtractTo
$target=if ($ExtractTo) { [IO.Path]::GetFullPath($ExtractTo) } else { Join-Path $temporaryRoot ('ExamGuardian-portable-check-'+[Guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $target) { throw 'Portable check requires a new extraction directory.' }
$saved=@{}
foreach ($name in @('PATH','QT_QPA_PLATFORM','QT_PLUGIN_PATH','QT_QPA_PLATFORM_PLUGIN_PATH','QML_IMPORT_PATH','QML2_IMPORT_PATH')) {
    $saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
}
try {
    Expand-Archive -LiteralPath $archive -DestinationPath $target
    foreach ($relative in @('Online_Proctoring_System_Client.exe','Qt6Core.dll','Qt6Gui.dll','Qt6Qml.dll','Qt6Quick.dll','Qt6Network.dll','Qt6Multimedia.dll','platforms/qwindows.dll','platforms/qoffscreen.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $target $relative) -PathType Leaf)) { throw "Incomplete portable archive: $relative" }
    }
    # Installed Qt/MinGW and developer QML paths must not mask missing files.
    $env:PATH="$env:SystemRoot/system32;$env:SystemRoot"
    $env:QT_QPA_PLATFORM='offscreen'
    foreach ($name in @('QT_PLUGIN_PATH','QT_QPA_PLATFORM_PLUGIN_PATH','QML_IMPORT_PATH','QML2_IMPORT_PATH')) {
        [Environment]::SetEnvironmentVariable($name,$null,'Process')
    }
    $arguments=@('--smoke-test')
    if ($Flavor -eq 'production') { $arguments+='--verify-production' }
    foreach ($argument in $arguments) {
        $process=Start-Process -FilePath (Join-Path $target 'Online_Proctoring_System_Client.exe') -WorkingDirectory $target -ArgumentList $argument -WindowStyle Hidden -PassThru
        try {
            if (!$process.WaitForExit(45000)) { $process.Kill(); $process.WaitForExit(); throw "Extracted portable client timed out: $argument" }
            if ($process.ExitCode -ne 0) { throw "Extracted portable client failed: $argument (exit $($process.ExitCode))" }
        } finally { $process.Dispose() }
    }
    Write-Output "Portable ZIP passed with no installed Qt on PATH: $archive"
    if (!$cleanup) { Write-Output "Ready to run: $target/Online_Proctoring_System_Client.exe" }
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') }
    if ($cleanup -and (Test-Path -LiteralPath $target)) {
        $resolved=[IO.Path]::GetFullPath((Resolve-Path -LiteralPath $target).Path)
        if ([IO.Path]::GetDirectoryName($resolved) -ne $temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) -or [IO.Path]::GetFileName($resolved) -notlike 'ExamGuardian-portable-check-*') {
            throw 'Refusing to clean an unexpected extraction directory.'
        }
        # Windows/antivirus may briefly retain the just-exited executable.
        for ($attempt=0; $attempt -lt 6; $attempt++) {
            try { Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction Stop; break }
            catch {
                if ($attempt -eq 5) { Write-Warning "Portable checks finished, but temporary files remain at $resolved" }
                else { Start-Sleep -Milliseconds 300 }
            }
        }
    }
}

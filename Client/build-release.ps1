param([ValidateSet("development","production")][string]$Flavor="production", [string]$QtRoot = 'C:/Qt/6.10.1/mingw_64', [string]$CompilerRoot = 'C:/Qt/Tools/mingw1310_64', [string]$OutputDirectory, [string]$InnoCompiler, [string]$NinjaPath, [string]$BuildDirectory)
$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $source ('releases/' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory. Existing releases are preserved.' }
foreach ($tool in @("$QtRoot/bin/windeployqt.exe", "$CompilerRoot/bin/g++.exe")) {
    if (!(Test-Path -LiteralPath $tool)) { throw "Required build tool not found: $tool" }
}
$env:PATH = "$QtRoot/bin;$CompilerRoot/bin;" + $env:PATH
if (!$NinjaPath) {
    $ninja=Get-Command ninja -ErrorAction SilentlyContinue
    if ($ninja) { $NinjaPath=$ninja.Source } else { $NinjaPath=Join-Path (Split-Path $CompilerRoot -Parent) 'Ninja/ninja.exe' }
}
if (!(Test-Path -LiteralPath $NinjaPath)) { throw 'Ninja not found. Pass -NinjaPath explicitly.' }
$build = if($BuildDirectory){[IO.Path]::GetFullPath($BuildDirectory)}else{Join-Path $source ('build/verified-' + $Flavor)}
cmake -S $source -B $build -G Ninja -DCMAKE_BUILD_TYPE=Release "-DEXAMGUARDIAN_FLAVOR=$Flavor" "-DCMAKE_PREFIX_PATH=$QtRoot" "-DCMAKE_MAKE_PROGRAM=$NinjaPath" "-DCMAKE_CXX_COMPILER=$CompilerRoot/bin/g++.exe"
if ($LASTEXITCODE -ne 0) { throw 'Configure failed.' }
cmake --build $build --parallel 2
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$deploy = Join-Path $output 'client'
New-Item -ItemType Directory -Path $deploy -Force | Out-Null
$exe = Join-Path $deploy 'Online_Proctoring_System_Client.exe'
Copy-Item -LiteralPath (Join-Path $build 'appClient.exe') -Destination $exe
& "$QtRoot/bin/windeployqt.exe" --verbose 0 --release --qmldir (Join-Path $source 'qml') $exe
if ($LASTEXITCODE -ne 0) { throw 'Qt deployment failed.' }
# The headless startup check needs this platform plugin, which windeployqt omits by default.
Copy-Item -LiteralPath (Join-Path $QtRoot 'plugins/platforms/qoffscreen.dll') -Destination (Join-Path $deploy 'platforms/qoffscreen.dll')
foreach ($dll in @('libgcc_s_seh-1.dll','libstdc++-6.dll','libwinpthread-1.dll')) { Copy-Item -LiteralPath (Join-Path "$CompilerRoot/bin" $dll) -Destination $deploy }
$oldPlatform = $env:QT_QPA_PLATFORM
try {
    $env:QT_QPA_PLATFORM = 'offscreen'
    $process = Start-Process -FilePath $exe -ArgumentList '--smoke-test' -PassThru -WindowStyle Hidden
    if (!$process.WaitForExit(45000)) { $process.Kill(); throw 'Packaged client did not start in time.' }
    if ($process.ExitCode -ne 0) { throw 'Packaged QML startup failed.' }
    if($Flavor -eq 'production') {
        $probe=Start-Process -FilePath $exe -ArgumentList '--verify-production' -PassThru -WindowStyle Hidden
        if(!$probe.WaitForExit(15000) -or $probe.ExitCode -ne 0) {throw 'Production artifact contains development resources or policies.'}
    }
} finally { $env:QT_QPA_PLATFORM = $oldPlatform }
if ($InnoCompiler) {
    & $InnoCompiler /Qp "/DDeployDir=$deploy" "/DBuildFlavor=$Flavor" "/DReleaseOutput=$output" (Join-Path $source 'installer.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
}
$commit = git -C $source rev-parse HEAD
$dirty = [bool](git -C $source status --porcelain)
$sourceHashes = @{}
foreach ($relative in (git -C $source ls-files --cached --others --exclude-standard -- .)) {
    $file=Join-Path $source $relative
    if (Test-Path -LiteralPath $file -PathType Leaf) { $sourceHashes[$relative]=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
}
$files = Get-ChildItem -LiteralPath $output -File -Recurse | ForEach-Object { @{ path=$_.FullName.Substring($output.TrimEnd([IO.Path]::DirectorySeparatorChar).Length+1); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } }
@{flavor=$Flavor;commit=$commit; workingTreeModified=$dirty; builtUtc=[DateTime]::UtcNow.ToString('O'); protocol=4; qt=$QtRoot; sources=$sourceHashes; files=@($files)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'release-manifest.json') -Encoding utf8
Write-Output "Verified release: $output"

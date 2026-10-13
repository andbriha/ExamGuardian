@echo off
setlocal

echo ========================================================
echo       Online Proctoring System Client - Installer Builder
echo ========================================================

set "INNO_COMPILER="
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "INNO_COMPILER=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if "%INNO_COMPILER%"=="" if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "INNO_COMPILER=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if "%INNO_COMPILER%"=="" if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "INNO_COMPILER=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if "%INNO_COMPILER%"=="" goto :err_no_inno

echo [OK] Inno Setup compiler found: %INNO_COMPILER%

set "QT_BIN=C:\Qt\6.10.1\mingw_64\bin"
set "MINGW_BIN=C:\Qt\Tools\mingw1310_64\bin"
set "PATH=%QT_BIN%;%MINGW_BIN%;%PATH%"

set "WINDEPLOYQT=%QT_BIN%\windeployqt.exe"

set "SOURCE_EXE="
if exist ".\build\Desktop_Qt_6_10_1_MinGW_64_bit-Release\appClient.exe" set "SOURCE_EXE=.\build\Desktop_Qt_6_10_1_MinGW_64_bit-Release\appClient.exe"
if "%SOURCE_EXE%"=="" if exist ".\build\Desktop_Qt_6_10_1_MinGW_64_bit-Debug\appClient.exe" set "SOURCE_EXE=.\build\Desktop_Qt_6_10_1_MinGW_64_bit-Debug\appClient.exe"
if "%SOURCE_EXE%"=="" if exist "..\Landing\Online_Proctoring_System_Client.exe" set "SOURCE_EXE=..\Landing\Online_Proctoring_System_Client.exe"
if "%SOURCE_EXE%"=="" if exist "..\Landing\ExamGuardian_Client.exe" set "SOURCE_EXE=..\Landing\ExamGuardian_Client.exe"

if "%SOURCE_EXE%"=="" goto :err_no_source

echo [OK] Source executable: %SOURCE_EXE%

set "DEPLOY_DIR=.\Deploy"
echo [INFO] Preparing deploy folder...
if exist "%DEPLOY_DIR%" rmdir /S /Q "%DEPLOY_DIR%"
mkdir "%DEPLOY_DIR%"

copy /Y "%SOURCE_EXE%" "%DEPLOY_DIR%\Online_Proctoring_System_Client.exe" >nul
echo [INFO] Running windeployqt...
"%WINDEPLOYQT%" --qmldir .\qml "%DEPLOY_DIR%\Online_Proctoring_System_Client.exe"

if exist "%MINGW_BIN%\libgcc_s_seh-1.dll" copy /Y "%MINGW_BIN%\libgcc_s_seh-1.dll" "%DEPLOY_DIR%\" >nul
if exist "%MINGW_BIN%\libstdc++-6.dll" copy /Y "%MINGW_BIN%\libstdc++-6.dll" "%DEPLOY_DIR%\" >nul
if exist "%MINGW_BIN%\libwinpthread-1.dll" copy /Y "%MINGW_BIN%\libwinpthread-1.dll" "%DEPLOY_DIR%\" >nul

echo [INFO] Compiling installer with Inno Setup...
"%INNO_COMPILER%" installer.iss

if errorlevel 1 goto :err_compile

echo.
echo ========================================================
echo  SUCCESS: Installer created in ..\Landing\Online_Proctoring_System_Setup.exe
echo ========================================================
goto :end

:err_no_inno
echo [ERROR] Inno Setup compiler ISCC.exe not found.
echo Please install Inno Setup 6 from https://jrsoftware.org/isdl.php
pause
exit /b 1

:err_no_source
echo [ERROR] Source client executable appClient.exe not found.
pause
exit /b 1

:err_compile
echo [ERROR] Inno Setup compilation failed.
pause
exit /b 1

:end
endlocal
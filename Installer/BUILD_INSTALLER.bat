@echo off
title Klyr - Compilation + Installeur
color 0A
echo.
echo  ============================================
echo   Klyr v2.1 - Build complet + Installeur
echo  ============================================
echo.

REM Le .bat est dans installer/. On remonte d'un niveau vers la racine du projet.
cd /d "%~dp0.."
set "PROJECT_ROOT=%CD%"

echo [1/4] Compilation Release (self-contained, single file)...
REM Nettoyage du dossier publish pour eviter les anciens fichiers framework-dependent
if exist "publish" rmdir /s /q "publish"
dotnet publish Klyr.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishReadyToRun=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "publish"
if %errorlevel% neq 0 (
    echo [ERREUR] Compilation echouee.
    pause & exit /b 1
)
echo OK : runtime .NET 10 embarquee, aucun prerequis sur la machine cible.
echo.

echo [2/4] Localisation de NSIS (makensis.exe)...
set "MAKENSIS="
where makensis >nul 2>&1
if %errorlevel% equ 0 (
    set "MAKENSIS=makensis"
) else if exist "%ProgramFiles(x86)%\NSIS\makensis.exe" (
    set "MAKENSIS=%ProgramFiles(x86)%\NSIS\makensis.exe"
) else if exist "%ProgramFiles%\NSIS\makensis.exe" (
    set "MAKENSIS=%ProgramFiles%\NSIS\makensis.exe"
)
if "%MAKENSIS%"=="" (
    echo [INFO] NSIS introuvable.
    echo Telechargez NSIS : https://nsis.sourceforge.io/Download
    echo Installez-le avec les options par defaut puis relancez ce script.
    pause & exit /b 1
)
echo OK : %MAKENSIS%
echo.

echo [3/4] Creation de l'installeur (NSIS)...
cd /d "%PROJECT_ROOT%\installer"
"%MAKENSIS%" Klyr_Setup.nsi
if %errorlevel% neq 0 (
    echo [ERREUR] makensis a echoue.
    cd /d "%PROJECT_ROOT%"
    pause & exit /b 1
)
cd /d "%PROJECT_ROOT%"
echo OK
echo.

echo [4/4] Verification du resultat...
if exist "installer\Klyr_Setup_v2.1.0.exe" (
    move /Y "installer\Klyr_Setup_v2.1.0.exe" "Klyr_Setup_v2.1.0.exe" >nul
    echo.
    echo  ============================================
    echo   SUCCES !
    echo   Installeur : %PROJECT_ROOT%\Klyr_Setup_v2.1.0.exe
    echo  ============================================
) else (
    echo [ERREUR] Installeur introuvable apres compilation.
)
echo.
pause

@echo off
title InfoZen – Compilation + Installeur
color 0A
echo.
echo  ============================================
echo   InfoZen v2.1 – Build complet + Installeur
echo  ============================================
echo.

cd /d "%~dp0.."

echo [1/4] Compilation Release...
dotnet publish InfoZen.csproj -c Release --no-self-contained -o "publish"
if %errorlevel% neq 0 (
    echo [ERREUR] Compilation echouee.
    pause & exit /b 1
)
echo OK
echo.

echo [2/4] Verification de NSIS...
where makensis >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] NSIS introuvable dans le PATH.
    echo Telechargez NSIS : https://nsis.sourceforge.io/Download
    echo Installez-le puis relancez ce script.
    pause & exit /b 1
)
echo OK
echo.

echo [3/4] Creation de l installeur...
makensis InfoZen_Setup.nsi
if %errorlevel% neq 0 (
    echo [ERREUR] NSIS a echoue.
    cd ..
    pause & exit /b 1
)
cd ..
echo OK
echo.

echo [4/4] Resultat...
if exist "installer\InfoZen_Setup_v2.1.0.exe" (
    move /Y "installer\InfoZen_Setup_v2.1.0.exe" "InfoZen_Setup_v2.1.0.exe" >nul
    echo.
    echo  ============================================
    echo   SUCCES !
    echo   Installeur : InfoZen_Setup_v2.1.0.exe
    echo  ============================================
) else (
    echo [ERREUR] Installeur introuvable.
)
echo.
pause

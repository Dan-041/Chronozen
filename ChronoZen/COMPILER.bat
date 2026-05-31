@echo off
title ChronoZen — Compilation
color 0A
echo.
echo  ================================================
echo    ChronoZen — Compilation en cours...
echo  ================================================
echo.

:: Vérifier dotnet
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERREUR] .NET SDK introuvable.
    echo.
    echo  Installez le .NET 8 SDK (gratuit) :
    echo  https://dotnet.microsoft.com/download/dotnet/8.0
    echo  (choisir "SDK" puis "Windows x64")
    echo.
    pause
    exit /b 1
)

echo  .NET SDK detecte. Compilation...
echo.

dotnet publish ChronoZen.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o .\dist

if %errorlevel% neq 0 (
    echo.
    echo  [ERREUR] La compilation a echoue.
    echo  Verifiez que tous les fichiers sont presents.
    pause
    exit /b 1
)

echo.
echo  ================================================
echo    Succes ! L'application est dans : .\dist\
echo    Fichier : ChronoZen.exe
echo  ================================================
echo.
pause

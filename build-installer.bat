@echo off
setlocal enabledelayedexpansion
title Compilando instalador de Finanzas Personales

echo ================================================
echo  Finanzas Personales — Build del instalador
echo ================================================
echo.

:: ── 1. Publicar la aplicacion ────────────────────────────────────────────────
echo [1/2] Publicando aplicacion (self-contained, win-x64)...
dotnet publish "%~dp0FinanzasPersonales.csproj" ^
    -p:PublishProfile=win-x64-installer ^
    --nologo
if errorlevel 1 (
    echo ERROR: Fallo la publicacion de la aplicacion.
    pause & exit /b 1
)
echo     OK — Archivos publicados en: %~dp0publish\
echo.

:: ── 2. Compilar el instalador ────────────────────────────────────────────────
echo [2/2] Compilando instalador con Inno Setup...
mkdir "%~dp0Installer\Output" 2>nul
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "%~dp0Installer\setup.iss" /Q
if errorlevel 1 (
    echo ERROR: Fallo la compilacion del instalador.
    pause & exit /b 1
)

echo.
echo ================================================
echo  LISTO!
echo  Instalador generado en:
echo  %~dp0Installer\Output\FinanzasPersonales-Setup.exe
echo ================================================
echo.
pause

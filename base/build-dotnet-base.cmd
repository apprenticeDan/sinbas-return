@echo off
setlocal

:: Script para Windows (Batch)

cd /d "%~dp0"

set VERSION=%1
set REGISTRY=%2

if "%VERSION%"=="" set VERSION=latest
if "%REGISTRY%"=="" set REGISTRY=sinbas

set IMAGE_NAME=dotnet10-solid-dev
set FULL_IMAGE=%REGISTRY%/%IMAGE_NAME%

:: Detectamos si el usuario usa Podman o Docker
set DOCKER_CMD=docker
where podman >nul 2>nul
if %ERRORLEVEL% equ 0 set DOCKER_CMD=podman

%DOCKER_CMD% image inspect "%FULL_IMAGE%:%VERSION%" >nul 2>nul
if %ERRORLEVEL% equ 0 (
    if NOT "%3"=="--force" (
        if NOT "%1"=="--force" (
            echo [INFO] La imagen %FULL_IMAGE%:%VERSION% ya esta disponible localmente (%DOCKER_CMD%).
            echo        Usa '--force' si deseas forzar la reconstruccion.
            exit /b 0
        )
    )
)

echo [BUILD] Construyendo %FULL_IMAGE%:%VERSION% con %DOCKER_CMD% ...

%DOCKER_CMD% build -f Containerfile.dotnet.base --build-arg USERNAME=dev -t "%FULL_IMAGE%:%VERSION%" -t "%FULL_IMAGE%:latest" .

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Hubo un problema al construir la imagen.
    exit /b %ERRORLEVEL%
)

echo.
echo ===============================
echo Imagen creada exitosamente:
echo   %FULL_IMAGE%:%VERSION%
echo   %FULL_IMAGE%:latest
echo ===============================


@echo off
setlocal

:: Script para Windows (Batch)

set VERSION=%1
set REGISTRY=%2

if "%VERSION%"=="" (
    echo ERROR: Debes especificar una version.
    echo Uso: build-dotnet-base.cmd ^<version^> [registro]
    echo Ejemplo: build-dotnet-base.cmd 1.0.0
    exit /b 1
)

if "%REGISTRY%"=="" set REGISTRY=sinbas

set IMAGE_NAME=dotnet10-solid-dev
set FULL_IMAGE=%REGISTRY%/%IMAGE_NAME%

echo Construyendo %FULL_IMAGE%:%VERSION% ...

:: Detectamos si el usuario usa Podman o Docker
set DOCKER_CMD=docker
where podman >nul 2>nul
if %ERRORLEVEL% equ 0 set DOCKER_CMD=podman

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

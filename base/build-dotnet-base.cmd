@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "VERSION=latest"
set "REGISTRY=sinbas"
set "FORCE=0"
set "CHECK_ONLY=0"
set "NO_CACHE=0"
set "VERSION_ARG="
set "REGISTRY_ARG="

:parse_args
if "%~1"=="" goto args_done

if /I "%~1"=="--force"    goto opt_force
if /I "%~1"=="-f"         goto opt_force
if /I "%~1"=="--check"    goto opt_check
if /I "%~1"=="-c"         goto opt_check
if /I "%~1"=="--no-cache" goto opt_nocache
if /I "%~1"=="--help"     goto usage
if /I "%~1"=="-h"         goto usage

if not defined VERSION_ARG goto set_version
if not defined REGISTRY_ARG goto set_registry

echo [ERROR] Argumento desconocido: %~1
goto usage_error

:opt_force
set "FORCE=1"
shift
goto parse_args

:opt_check
set "CHECK_ONLY=1"
shift
goto parse_args

:opt_nocache
set "NO_CACHE=1"
shift
goto parse_args

:set_version
set "VERSION=%~1"
set "VERSION_ARG=1"
shift
goto parse_args

:set_registry
set "REGISTRY=%~1"
set "REGISTRY_ARG=1"
shift
goto parse_args

:args_done

set "IMAGE_NAME=dotnet10-solid-dev"
set "FULL_IMAGE=%REGISTRY%/%IMAGE_NAME%"
set "IMAGE_TAG=%FULL_IMAGE%:%VERSION%"

:: Detectar motor de contenedores
where podman >nul 2>nul
if not errorlevel 1 (
    set "DOCKER_CMD=podman"
    goto engine_found
)

where docker >nul 2>nul
if not errorlevel 1 (
    set "DOCKER_CMD=docker"
    goto engine_found
)

echo [ERROR] No se encontro Docker ni Podman en PATH.
exit /b 127

:engine_found

set "IMAGE_EXISTS=0"
%DOCKER_CMD% image inspect "%IMAGE_TAG%" >nul 2>nul
if not errorlevel 1 set "IMAGE_EXISTS=1"

if "%CHECK_ONLY%"=="1" goto do_check

if "%IMAGE_EXISTS%"=="1" if "%FORCE%"=="0" goto already_exists

goto do_build

:do_check
if "%IMAGE_EXISTS%"=="1" (
    echo [OK] La imagen %IMAGE_TAG% existe localmente.
    exit /b 0
)
echo [INFO] La imagen %IMAGE_TAG% no existe localmente.
exit /b 1

:already_exists
echo [INFO] La imagen %IMAGE_TAG% ya existe - motor: %DOCKER_CMD%
echo        Usa --force para reconstruirla.
exit /b 0

:do_build
echo [BUILD] Construyendo %IMAGE_TAG% con %DOCKER_CMD% ...

set "CACHE_FLAG="
if "%NO_CACHE%"=="1" set "CACHE_FLAG=--no-cache"

%DOCKER_CMD% build %CACHE_FLAG% -f Containerfile.dotnet.base --build-arg USERNAME=dev -t "%IMAGE_TAG%" -t "%FULL_IMAGE%:latest" .
if errorlevel 1 goto build_failed

echo.
echo ===============================
echo Imagen creada exitosamente:
echo   %IMAGE_TAG%
echo   %FULL_IMAGE%:latest
echo ===============================
exit /b 0

:build_failed
echo.
echo [ERROR] Hubo un problema al construir la imagen.
exit /b 1

:usage
echo Uso: %~nx0 [VERSION] [REGISTRY] [--force] [--check] [--no-cache]
echo.
echo Ejemplos:
echo   %~nx0
echo   %~nx0 1.0.0
echo   %~nx0 1.0.0 sinbas
echo   %~nx0 --force
echo   %~nx0 --check
echo   %~nx0 --no-cache --force
exit /b 0

:usage_error
echo.
echo Usa "%~nx0 --help" para ver los argumentos disponibles.
exit /b 2
#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

FORCE=false
CHECK_ONLY=false
VERSION="latest"
REGISTRY="sinbas"

for arg in "$@"; do
    case "$arg" in
        --force|-f)
            FORCE=true
            ;;
        --check|-c)
            CHECK_ONLY=true
            ;;
        *)
            if [ "$VERSION" = "latest" ]; then
                VERSION="$arg"
            else
                REGISTRY="$arg"
            fi
            ;;
    esac
done

IMAGE_NAME="dotnet10-solid-dev"
FULL_IMAGE="${REGISTRY}/${IMAGE_NAME}"

if command -v podman &> /dev/null; then
    DOCKER_CMD="podman"
else
    DOCKER_CMD="docker"
fi

if $DOCKER_CMD image inspect "${FULL_IMAGE}:${VERSION}" &>/dev/null; then
    IMAGE_EXISTS=true
else
    IMAGE_EXISTS=false
fi

if [ "$CHECK_ONLY" = true ]; then
    if [ "$IMAGE_EXISTS" = true ]; then
        echo "[OK] La imagen ${FULL_IMAGE}:${VERSION} ya existe localmente."
        exit 0
    else
        echo "[INFO] La imagen ${FULL_IMAGE}:${VERSION} NO existe localmente."
        exit 1
    fi
fi

if [ "$IMAGE_EXISTS" = true ] && [ "$FORCE" = false ]; then
    echo "[INFO] La imagen ${FULL_IMAGE}:${VERSION} ya está disponible en el motor local ($DOCKER_CMD)."
    echo "       Usa '--force' o '-f' si deseas forzar la reconstrucción."
    exit 0
fi

echo "[BUILD] Construyendo ${FULL_IMAGE}:${VERSION} con $DOCKER_CMD..."

$DOCKER_CMD build \
    -f Containerfile.dotnet.base \
    --build-arg USERNAME=dev \
    -t "${FULL_IMAGE}:${VERSION}" \
    -t "${FULL_IMAGE}:latest" \
    .

echo
echo "=========================================="
echo "Imagen base disponible exitosamente:"
echo "  ${FULL_IMAGE}:${VERSION}"
echo "  ${FULL_IMAGE}:latest"
echo "=========================================="

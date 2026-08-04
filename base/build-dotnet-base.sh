#!/usr/bin/env bash

set -euo pipefail

VERSION="${1:?Uso: ./build-dotnet-base.sh <version> [registro]}"

REGISTRY="${2:-sinbas}"
IMAGE_NAME="dotnet10-solid-dev"

FULL_IMAGE="${REGISTRY}/${IMAGE_NAME}"

echo "Construyendo ${FULL_IMAGE}:${VERSION}"

if command -v podman &> /dev/null; then
    DOCKER_CMD="podman"
else
    DOCKER_CMD="docker"
fi

$DOCKER_CMD build \
    -f Containerfile.dotnet.base \
    --build-arg USERNAME=dev \
    -t "${FULL_IMAGE}:${VERSION}" \
    -t "${FULL_IMAGE}:latest" \
    .

echo
echo "Imagen creada:"
echo "  ${FULL_IMAGE}:${VERSION}"
echo "  ${FULL_IMAGE}:latest"

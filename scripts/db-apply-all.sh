#!/usr/bin/env bash
# ============================================================
# db-apply-all.sh — Aplica todas las migraciones SQL en orden.
# Uso: bash scripts/db-apply-all.sh
# Requiere: podman corriendo + container DB activo.
# Es idempotente: re-ejecutarlo no rompe nada (las migraciones
# usan IF NOT EXISTS / ON CONFLICT DO NOTHING).
# ============================================================
set -euo pipefail

DB_CONTAINER="${DB_CONTAINER:-sinbasreturn_sinbas-db_1}"
DB_USER="${DB_USER:-sinbas}"
DB_NAME="${DB_NAME:-sinbas}"

cd "$(dirname "$0")/.."   # raíz del repo

echo "▸ Aplicando migraciones de db/*.sql en orden..."
for f in $(ls db/*.sql | sort); do
    echo "  → $(basename "$f")"
    podman exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" \
        -v ON_ERROR_STOP=1 -f - < "$f" \
        > /dev/null
done

echo "✓ Todas las migraciones aplicadas ($(ls db/*.sql | wc -l) archivos)"
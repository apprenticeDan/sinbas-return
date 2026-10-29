#!/usr/bin/env bash
# ============================================================
# db-full-reset.sh — Reset total: borra TODO (incluye catálogo,
# usuarios y empleados) y reaplica migraciones + seeds.
# Uso: bash scripts/db-full-reset.sh
# ============================================================
set -euo pipefail

DB_CONTAINER="${DB_CONTAINER:-sinbasreturn_sinbas-db_1}"
DB_USER="${DB_USER:-sinbas}"
DB_NAME="${DB_NAME:-sinbas}"

cd "$(dirname "$0")/.."

echo "▸ [1/3] TRUNCATE total (CASCADE)..."
podman exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" \
  -v ON_ERROR_STOP=1 <<'EOF'
BEGIN;
TRUNCATE TABLE linea_movimiento, linea_despacho, linea_orden_venta,
               linea_proforma, analisis_laboratorio, reserva_stock,
               solicitud_egreso_interno, movimiento_inventario,
               orden_despacho, orden_venta, proforma, lote,
               cliente, producto, usuario_rol, refresh_token,
               registro_idempotencia, usuario, empleado
CASCADE;
COMMIT;
EOF

echo "▸ [2/3] Reaplicando migraciones..."
bash scripts/db-apply-all.sh

echo "▸ [3/3] Verificando..."
podman exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c "
SELECT 'producto' t, COUNT(*) FROM producto
UNION ALL SELECT 'usuario', COUNT(*) FROM usuario
UNION ALL SELECT 'empleado', COUNT(*) FROM empleado
UNION ALL SELECT 'lote', COUNT(*) FROM lote;"

echo "✓ Reset total completado"
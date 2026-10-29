-- ============================================================
-- db-reset-operational.sql
-- Borra SOLO datos operativos (lotes, movimientos, proformas,
-- ventas, despachos, reservas, solicitudes, análisis, clientes).
-- PRESERVA: usuario, empleado, producto (catálogo), migrations.
--
-- USO: desarrollo. Reutilizable múltiples veces.
-- NO ejecutar en producción.
-- ============================================================

BEGIN;

-- Orden FK-safe: hijas antes que padres
TRUNCATE TABLE linea_movimiento         CASCADE;
TRUNCATE TABLE linea_despacho           CASCADE;
TRUNCATE TABLE linea_orden_venta        CASCADE;
TRUNCATE TABLE linea_proforma           CASCADE;
TRUNCATE TABLE analisis_laboratorio     CASCADE;
TRUNCATE TABLE reserva_stock            CASCADE;
TRUNCATE TABLE solicitud_egreso_interno CASCADE;
TRUNCATE TABLE movimiento_inventario    CASCADE;
TRUNCATE TABLE orden_despacho           CASCADE;
TRUNCATE TABLE orden_venta              CASCADE;
TRUNCATE TABLE proforma                 CASCADE;
TRUNCATE TABLE lote                     CASCADE;

-- Cliente es opcional: comentá esta línea si querés preservarlos
TRUNCATE TABLE cliente                  CASCADE;

-- Verificación: todas las tablas operativas en 0
DO $$
DECLARE
  n int;
BEGIN
  SELECT
    (SELECT COUNT(*) FROM lote) +
    (SELECT COUNT(*) FROM movimiento_inventario) +
    (SELECT COUNT(*) FROM proforma) +
    (SELECT COUNT(*) FROM orden_venta) +
    (SELECT COUNT(*) FROM orden_despacho) +
    (SELECT COUNT(*) FROM reserva_stock) +
    (SELECT COUNT(*) FROM solicitud_egreso_interno) +
    (SELECT COUNT(*) FROM analisis_laboratorio) +
    (SELECT COUNT(*) FROM cliente)
  INTO n;

  IF n <> 0 THEN
    RAISE EXCEPTION 'Reset incompleto: % filas operativas quedaron', n;
  END IF;

  RAISE NOTICE 'Reset operativo OK. Preservados: % productos, % usuarios, % empleados',
    (SELECT COUNT(*) FROM producto),
    (SELECT COUNT(*) FROM usuario),
    (SELECT COUNT(*) FROM empleado);
END $$;

COMMIT;
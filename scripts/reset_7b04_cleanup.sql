-- ============================================================
-- reset_7b04_cleanup.sql
-- Limpia datos operativos de prueba y borra 6 productos basura
-- (duplicados con observaciones "Diagnóstico" y "para test").
-- PRESERVA: usuarios, empleados, clientes y 8 productos válidos.
-- SOLO dev. NO ejecutar en prod.
-- ============================================================

BEGIN;

-- ── Paso 1: TRUNCATE operativo (hijas primero) ──────────────
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

-- ── Paso 2: Borrar productos basura (ya sin dependencias) ───
-- Solo los 6 duplicados sin uso real.
DELETE FROM producto WHERE id IN (
  '01a17a09-252f-7386-a048-048b5423b9bf',  -- Pinus plano #1
  '01a17a27-b002-7b19-bc79-343e8cad2dd1',  -- Pinus plano #2
  '01a17a09-2536-70ee-ae9c-a4ba728af532',  -- Pinus value #1
  '01a17a27-b007-72d7-991c-9d63ed9cefe9',  -- Pinus value #2
  '01a17a09-1f20-74a8-94b4-4fa474086bb4',  -- Polylepis incana #1
  '01a17a27-ab2f-7d8d-b3f9-18c556d753c4'   -- Polylepis incana #2
);

-- ── Paso 3: Verificación ────────────────────────────────────
DO $$
DECLARE
  n_prod      int;
  n_dup       int;
  n_operativa int;
BEGIN
  SELECT COUNT(*) INTO n_prod FROM producto;
  IF n_prod <> 8 THEN
    RAISE EXCEPTION 'Esperaba 8 productos tras cleanup, hay %', n_prod;
  END IF;

  SELECT COUNT(*) INTO n_dup FROM (
    SELECT LOWER(genero), LOWER(epiteto)
    FROM producto
    WHERE categoria IN ('Semilla','Plantin')
      AND genero IS NOT NULL AND epiteto IS NOT NULL
    GROUP BY LOWER(genero), LOWER(epiteto)
    HAVING COUNT(*) > 1
  ) sub;
  IF n_dup > 0 THEN
    RAISE EXCEPTION 'Aún hay % pares de duplicados, ABORT', n_dup;
  END IF;

  SELECT
    (SELECT COUNT(*) FROM lote) +
    (SELECT COUNT(*) FROM movimiento_inventario) +
    (SELECT COUNT(*) FROM proforma) +
    (SELECT COUNT(*) FROM orden_venta) +
    (SELECT COUNT(*) FROM solicitud_egreso_interno)
  INTO n_operativa;
  IF n_operativa <> 0 THEN
    RAISE EXCEPTION 'Tablas operativas no vacías: % filas totales', n_operativa;
  END IF;

  RAISE NOTICE 'Reset OK: % productos, 0 operativas, 0 duplicados', n_prod;
END $$;

COMMIT;
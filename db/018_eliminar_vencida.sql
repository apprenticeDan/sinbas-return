-- ============================================================
-- 018_eliminar_vencida.sql — GATE F7: Eliminación de estado 'Vencida'
-- Las proformas de BASFOR son documentos informativos sin compromiso
-- ni vencimiento.
-- ============================================================

-- 1. Actualizar explícitamente cualquier fila con estado 'Vencida' a 'Vigente'
UPDATE proforma
SET estado = 'Vigente'
WHERE estado = 'Vencida';

-- 2. Actualizar constraint de chequeo para excluir 'Vencida'
ALTER TABLE proforma DROP CONSTRAINT IF EXISTS ck_proforma_estado;
ALTER TABLE proforma ADD CONSTRAINT ck_proforma_estado
    CHECK (estado IN ('Vigente', 'Convertida', 'Anulada'));


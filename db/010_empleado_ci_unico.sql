-- ============================================================
-- 010_empleado_ci_unico.sql — Unicidad de CI en Empleado (Número + Complemento)
--
-- Regla: Dos personas no pueden compartir el mismo documento de identidad.
-- Si dos personas tienen el mismo número de carnet pero complementos distintos
-- (asignados por SEGIP para dirimir homonimias), se consideran documentos diferentes.
-- Por tanto, la unicidad se evalúa sobre (ci_numero, COALESCE(UPPER(ci_complemento), '')).
-- ============================================================

CREATE UNIQUE INDEX IF NOT EXISTS ix_empleado_ci_unico
    ON empleado (ci_numero, COALESCE(UPPER(ci_complemento), ''))
    WHERE ci_numero IS NOT NULL AND ci_numero <> '-' AND ci_numero <> '';

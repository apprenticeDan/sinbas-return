-- ============================================================
-- 017_catalogo_unique_nc.sql — Unicidad de nombre científico
-- Evita crear productos Semilla/Plantin duplicados por (genero, epiteto).
-- Insumos quedan fuera del índice (no tienen genero/epiteto).
-- ============================================================

CREATE UNIQUE INDEX IF NOT EXISTS ux_producto_genero_epiteto
ON producto (LOWER(genero), LOWER(epiteto))
WHERE genero IS NOT NULL AND epiteto IS NOT NULL;

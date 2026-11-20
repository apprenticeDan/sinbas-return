-- Migración 022: Refactorización de unicidad en catálogo de productos (V1 / V3 - Caso B)
--
-- Reemplaza el índice único que bloqueaba género+epíteto sin importar categoría.
-- Permite tener la misma especie como Semilla y como Plantín, manteniendo
-- el bloqueo de duplicados dentro de la misma categoría.
--
-- Para insumos, crea un índice único separado sobre (nombre_insumo, marca_insumo).
--
-- PREPARADA para Fase 3 (no aplicar aún hasta validación formal).

DROP INDEX IF EXISTS ux_producto_genero_epiteto;

-- Semilla / Plantín: misma especie permitida en categorías distintas
CREATE UNIQUE INDEX ux_producto_genero_epiteto_categoria
ON producto (LOWER(genero), LOWER(epiteto), categoria)
WHERE genero IS NOT NULL AND epiteto IS NOT NULL;

-- Insumo: mismo nombre_insumo permitido con distinta marca, pero no duplicado exacto
CREATE UNIQUE INDEX ux_producto_insumo
ON producto (LOWER(nombre_insumo), LOWER(COALESCE(marca_insumo, '')))
WHERE categoria = 'Insumo' AND nombre_insumo IS NOT NULL;


-- Migración 003: Constraint de integridad sobre cantidad_reservada en lote
-- Previene corrupción silenciosa: reserva nunca negativa y nunca mayor al stock físico.
-- PRE-REQUISITO: Verificar que no hay filas inconsistentes con:
--   SELECT * FROM lote WHERE cantidad_reservada > cantidad_actual OR cantidad_reservada < 0;
-- Si retorna filas, corregir manualmente antes de aplicar.

ALTER TABLE lote
  ADD CONSTRAINT chk_cantidad_reservada_range
  CHECK (cantidad_reservada >= 0 AND cantidad_reservada <= cantidad_actual);

-- Rollback:
-- ALTER TABLE lote DROP CONSTRAINT chk_cantidad_reservada_range;

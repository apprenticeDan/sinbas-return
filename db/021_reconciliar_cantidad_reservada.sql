-- Migración 021: Reconciliar cantidad_reservada en lote con reserva_stock activa
--
-- Problema: lote.cantidad_reservada puede desincronizarse de las filas activas en
-- reserva_stock por deploys previos, resets de DB o bugs en creación de solicitudes.
-- Esta migración corrige todos los lotes afectados de una sola vez.
--
-- Es segura: solo actualiza filas donde hay discrepancia real.
-- Fuente de verdad: reserva_stock (tabla de reservas documentadas).
--
-- Aplicar ANTES de Fase 3. No tiene efecto si no hay desincronizaciones.

UPDATE lote l
SET cantidad_reservada = COALESCE((
    SELECT SUM(r.cantidad)
    FROM reserva_stock r
    WHERE r.lote_id = l.id
      AND r.estado = 'Activa'
), 0)
WHERE l.cantidad_reservada <> COALESCE((
    SELECT SUM(r.cantidad)
    FROM reserva_stock r
    WHERE r.lote_id = l.id
      AND r.estado = 'Activa'
), 0);


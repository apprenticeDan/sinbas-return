-- Migración 015: FK reserva_stock.movimiento_id pasa a DEFERRABLE
-- Motivo: permitir reordenar INSERT/UPDATE dentro de una transacción
-- sin violar la FK. Elimina la clase entera de bugs de orden.

ALTER TABLE reserva_stock
    DROP CONSTRAINT IF EXISTS reserva_stock_movimiento_id_fkey;

ALTER TABLE reserva_stock
    ADD CONSTRAINT reserva_stock_movimiento_id_fkey
    FOREIGN KEY (movimiento_id) REFERENCES movimiento_inventario(id)
    DEFERRABLE INITIALLY DEFERRED;

-- Fin migración 015.

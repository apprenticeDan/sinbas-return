-- Migración 016: relajar chk_sol_estado_resolucion para permitir
-- que las solicitudes Confirmadas conserven el audit trail de la aprobación.
--
-- Motivación: el flujo Aprobada → Confirmada no borra resolucion_* en DB,
-- pero el CHECK original exigía NULL para Confirmada. Bug detectado en smoke
-- test Fase 6b (23514 en confirmar). El diseño del dominio
-- (EstadoSolicitudInterna.Confirmada) tampoco lleva ResolucionSolicitud,
-- por lo que la resolución se pierde en el dominio pero debe preservarse
-- en DB para auditoría legal.
--
-- Cambios:
-- - Pendiente: sin cambios (todo NULL)
-- - Aprobada:  sin cambios (resolución obligatoria, sin entrega)
-- - Rechazada: sin cambios (resolución con motivo, sin entrega)
-- - Confirmada: 
--     * UsoInterno: sin resolución (nunca pasó por aprobación)
--     * Merma/Ajuste: CON resolución (audit trail del aprobador)

ALTER TABLE solicitud_egreso_interno 
  DROP CONSTRAINT IF EXISTS chk_sol_estado_resolucion;

ALTER TABLE solicitud_egreso_interno
  ADD CONSTRAINT chk_sol_estado_resolucion CHECK (
    (estado = 'Pendiente'
        AND resolucion_aprobador_id IS NULL
        AND resolucion_fecha IS NULL
        AND resolucion_motivo IS NULL
        AND entregado_por IS NULL
        AND fecha_entrega IS NULL) OR
    (estado = 'Aprobada'
        AND resolucion_aprobador_id IS NOT NULL
        AND resolucion_fecha IS NOT NULL
        AND resolucion_motivo IS NULL
        AND entregado_por IS NULL
        AND fecha_entrega IS NULL) OR
    (estado = 'Rechazada'
        AND resolucion_aprobador_id IS NOT NULL
        AND resolucion_fecha IS NOT NULL
        AND resolucion_motivo IS NOT NULL
        AND entregado_por IS NULL
        AND fecha_entrega IS NULL) OR
    (estado = 'Confirmada'
        AND entregado_por IS NOT NULL
        AND fecha_entrega IS NOT NULL
        AND (
            (tipo = 'UsoInterno'
                AND resolucion_aprobador_id IS NULL
                AND resolucion_fecha IS NULL
                AND resolucion_motivo IS NULL) OR
            (tipo IN ('Merma', 'Ajuste')
                AND resolucion_aprobador_id IS NOT NULL
                AND resolucion_fecha IS NOT NULL
                AND resolucion_motivo IS NULL)
        ))
  );

-- Fin migración 016.
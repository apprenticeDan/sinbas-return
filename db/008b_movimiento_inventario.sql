-- ============================================================
-- 008b_movimiento_inventario.sql — Movimientos de Inventario (F4 / F5 / F8 / F9)
-- ============================================================

create table if not exists movimiento_inventario (
    id                  uuid primary key,
    fecha               timestamp with time zone not null default current_timestamp,
    responsable_id      uuid not null references empleado(id) on delete restrict,
    tipo                text not null, -- 'Entrada' | 'Salida'
    motivo              text not null, -- 'Compra', 'Recoleccion', 'Donacion', 'Devolucion', 'Venta', 'UsoInterno', 'Merma', 'Trueque', etc.
    orden_origen_id     uuid,          -- OrdenId opcional
    contraparte_ref     uuid,          -- ID de cliente/proveedor (preparado para F6 Clientes)
    contraparte_nombre  text,          -- Nombre/consignatario en texto libre (soporta búsqueda multivariable)
    departamento        text,          -- Para UsoInterno: depto/área solicitante (cliente interno)
    solicitante         text,          -- Para UsoInterno: nombre/id de quien solicita
    observaciones       text
);

create index if not exists ix_movimiento_fecha on movimiento_inventario(fecha);
create index if not exists ix_movimiento_tipo on movimiento_inventario(tipo);
create index if not exists ix_movimiento_motivo on movimiento_inventario(motivo);

create table if not exists linea_movimiento (
    id                  uuid primary key,
    movimiento_id       uuid not null references movimiento_inventario(id) on delete cascade,
    lote_id             uuid not null references lote(id) on delete restrict,
    cantidad            numeric(12,2) not null,
    unidad              text not null, -- 'Gramo' | 'Kilogramo' | 'Unidad_'
    observaciones       text
);

create index if not exists ix_linea_movimiento_id on linea_movimiento(movimiento_id);
create index if not exists ix_linea_lote_id on linea_movimiento(lote_id);

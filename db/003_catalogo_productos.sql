-- ============================================================
-- 003_catalogo_productos.sql — Catálogo Oficial de Productos
-- ============================================================

create table if not exists producto (
    id                  uuid primary key,
    categoria           text not null, -- 'Semilla', 'Plantin', 'Insumo', 'Otro'
    genero              text,
    epiteto             text,
    observaciones_nc    text,
    nombres_comunes     text, -- comas separadas o texto
    etapa_desarrollo    text,
    nombre_insumo       text,
    marca_insumo        text,
    descripcion_insumo  text,
    unidad_manejo       text not null, -- 'Gramo', 'Kilogramo', 'Unidad_', 'Bolsa'
    gramos_nominales    numeric(12,2),
    trazabilidad        text not null, -- 'PorLote', 'Simple'
    precio_oficial      numeric(12,2),
    precio_moneda       text default 'BOB',
    precio_fecha        timestamp with time zone,
    precio_usuario_id   uuid references usuario(id),
    estado_comercial    text not null default 'PendientePrecioBorrador', -- 'PendientePrecioBorrador', 'ActivoParaVenta', 'Inactivo'
    activo              boolean not null default true,
    observaciones       text
);

create index if not exists ix_producto_categoria on producto(categoria);
create index if not exists ix_producto_estado on producto(estado_comercial);

-- Seed de productos de prueba
insert into producto (
    id, categoria, genero, epiteto, observaciones_nc, nombres_comunes,
    unidad_manejo, trazabilidad, precio_oficial, precio_moneda, precio_fecha,
    estado_comercial, activo, observaciones
)
values (
    '01917f3a-0003-7000-8000-000000000001',
    'Semilla',
    'Swietenia',
    'macrophylla',
    null,
    'Caoba, Mara',
    'Kilogramo',
    'PorLote',
    150.00,
    'BOB',
    now(),
    'ActivoParaVenta',
    true,
    'Semilla forestal de alta calidad'
)
on conflict (id) do nothing;

insert into producto (
    id, categoria, genero, epiteto, observaciones_nc, nombres_comunes,
    unidad_manejo, trazabilidad, precio_oficial, precio_moneda, precio_fecha,
    estado_comercial, activo, observaciones
)
values (
    '01917f3a-0003-7000-8000-000000000002',
    'Semilla',
    'Cedrela',
    'odorata',
    null,
    'Cedro',
    'Kilogramo',
    'PorLote',
    null,
    'BOB',
    null,
    'PendientePrecioBorrador',
    true,
    'Lote recién ingresado por Almacén sin precio oficial'
)
on conflict (id) do nothing;

insert into producto (
    id, categoria, genero, epiteto, observaciones_nc, nombres_comunes,
    unidad_manejo, trazabilidad, precio_oficial, precio_moneda, precio_fecha,
    estado_comercial, activo, observaciones
)
values (
    '01917f3a-0003-7000-8000-000000000003',
    'Semilla',
    'Handroanthus',
    'impetiginosus',
    null,
    'Tajibo Morado',
    'Gramo',
    'PorLote',
    2.50,
    'BOB',
    now(),
    'ActivoParaVenta',
    true,
    'Especie ornamental y maderable'
)
on conflict (id) do nothing;

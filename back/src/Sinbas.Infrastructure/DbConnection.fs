namespace Sinbas.Infrastructure

open System
open Npgsql
open Dapper.FSharp.PostgreSQL

open Dapper
open System.Data

type DateOnlyTypeHandler() =
    inherit SqlMapper.TypeHandler<DateOnly>()
    override _.SetValue(param: IDbDataParameter, value: DateOnly) =
        param.DbType <- DbType.Date
        param.Value <- value
    override _.Parse(value: obj) : DateOnly =
        match value with
        | :? DateOnly as d -> d
        | :? DateTime as dt -> DateOnly.FromDateTime(dt)
        | :? string as s -> DateOnly.Parse(s)
        | _ -> Convert.ToDateTime(value) |> DateOnly.FromDateTime

type DateTimeTypeHandler() =
    inherit SqlMapper.TypeHandler<DateTime>()
    override _.SetValue(param: IDbDataParameter, value: DateTime) =
        param.DbType <- DbType.DateTime
        param.Value <- value
    override _.Parse(value: obj) : DateTime =
        match value with
        | :? DateTime as dt -> dt
        | :? DateOnly as d -> d.ToDateTime(TimeOnly.MinValue)
        | :? string as s -> DateTime.Parse(s)
        | _ -> Convert.ToDateTime(value)

module DbConnection =

    /// Lee la cadena de conexión desde DATABASE_URL (variable de entorno).
    /// Fallback para desarrollo local dentro del contenedor.
    let private connectionString () =
        Environment.GetEnvironmentVariable("DATABASE_URL")
        |> Option.ofObj
        |> Option.defaultValue "Host=sinbas-db;Database=sinbas;Username=sinbas;Password=sinbas"

    /// Crea una conexión abierta a PostgreSQL.
    let crear () : NpgsqlConnection =
        new NpgsqlConnection(connectionString ())

    /// Inicializa las tablas y datos semilla de autenticación si no existen.
    let inicializar () =
        try
            // Registrar mapeo automático para F# Option en Dapper.FSharp
            OptionTypes.register()
            SqlMapper.AddTypeHandler(DateOnlyTypeHandler())
            SqlMapper.AddTypeHandler(DateTimeTypeHandler())

            use conn = crear ()
            conn.Open()

            let sqlAuth = """
create table if not exists empleado (
    id              uuid primary key,
    nombres         text,
    apellido_paterno text,
    apellido_materno text,
    ci_numero       text,
    ci_complemento  text,
    telefono        text,
    email           text,
    nombre_completo text    not null,
    estado          text    not null default 'Activo'
);

alter table empleado add column if not exists nombres text;
alter table empleado add column if not exists apellido_paterno text;
alter table empleado add column if not exists apellido_materno text;
alter table empleado add column if not exists ci_numero text;
alter table empleado add column if not exists ci_complemento text;
alter table empleado add column if not exists telefono text;
alter table empleado add column if not exists email text;

create table if not exists usuario (
    id              uuid primary key,
    empleado_id     uuid    not null references empleado(id) on delete cascade,
    nombre_usuario  text    not null unique,
    password_hash   text    not null,
    estado          text    not null default 'Activo'
);

create index if not exists ix_usuario_nombre
    on usuario(nombre_usuario);

create table if not exists usuario_rol (
    usuario_id  uuid    not null references usuario(id) on delete cascade,
    rol         text    not null,
    primary key (usuario_id, rol)
);

insert into empleado (id, nombres, apellido_paterno, apellido_materno, ci_numero, ci_complemento, telefono, email, nombre_completo, estado)
values ('01917f3a-0001-7000-8000-000000000001', 'Administrador', 'del Sistema', null, '1234567', null, null, null, 'del Sistema, Administrador', 'Activo')
on conflict (id) do update set
    nombres = coalesce(empleado.nombres, excluded.nombres),
    apellido_paterno = coalesce(empleado.apellido_paterno, excluded.apellido_paterno),
    ci_numero = coalesce(empleado.ci_numero, excluded.ci_numero);

insert into usuario (id, empleado_id, nombre_usuario, password_hash, estado)
values (
    '01917f3a-0002-7000-8000-000000000002',
    '01917f3a-0001-7000-8000-000000000001',
    'admin',
    '$2a$11$8bv8pfyb92XqSnbzqIP9vuvXcnfCotYVp6Svj6ASNbYJTftltLBmu',
    'Activo'
)
on conflict (nombre_usuario) do nothing;

insert into usuario_rol (usuario_id, rol)
values (
    '01917f3a-0002-7000-8000-000000000002',
    'Administrador'
)
on conflict (usuario_id, rol) do nothing;

create table if not exists producto (
    id                  uuid primary key,
    categoria           text not null,
    genero              text,
    epiteto             text,
    observaciones_nc    text,
    nombres_comunes     text,
    etapa_desarrollo    text,
    nombre_insumo       text,
    marca_insumo        text,
    descripcion_insumo  text,
    unidad_manejo       text not null,
    gramos_nominales    numeric(12,2),
    trazabilidad        text not null,
    precio_oficial      numeric(12,2),
    precio_moneda       text default 'BOB',
    precio_fecha        timestamp with time zone,
    precio_usuario_id   uuid references usuario(id),
    estado_comercial    text not null default 'PendientePrecioBorrador',
    activo              boolean not null default true,
    observaciones       text
);

create index if not exists ix_producto_categoria on producto(categoria);
create index if not exists ix_producto_estado on producto(estado_comercial);

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

create table if not exists lote (
    id                  uuid primary key,
    codigo              text not null unique,
    producto_id         uuid not null references producto(id) on delete restrict,
    procedencia         text,
    cantidad_inicial    numeric(12,2) not null,
    cantidad_actual     numeric(12,2) not null,
    unidad              text not null,
    fecha_ingreso       date not null default current_date,
    ubicacion           text,
    estado              text not null default 'Activo',
    observaciones       text
);

create index if not exists ix_lote_producto_id on lote(producto_id);
create index if not exists ix_lote_estado on lote(estado);
create index if not exists ix_lote_codigo on lote(codigo);

insert into lote (
    id, codigo, producto_id, procedencia, cantidad_inicial, cantidad_actual,
    unidad, fecha_ingreso, ubicacion, estado, observaciones
)
values (
    '01917f3a-0004-7000-8000-000000000001',
    'SWIETMAC-02608-01',
    '01917f3a-0003-7000-8000-000000000001',
    'Bosque Chiquitano - Reserva Don Mario',
    50.00,
    50.00,
    'Kilogramo',
    '2026-08-15',
    'Almacén Central - Estante A1',
    'Activo',
    'Semilla recolectada en temporada alta con buena viabilidad'
)
on conflict (id) do nothing;

insert into lote (
    id, codigo, producto_id, procedencia, cantidad_inicial, cantidad_actual,
    unidad, fecha_ingreso, ubicacion, estado, observaciones
)
values (
    '01917f3a-0004-7000-8000-000000000002',
    'HANDIMPE-02608-01',
    '01917f3a-0003-7000-8000-000000000003',
    'Vivero Municipal Santa Cruz',
    2500.00,
    1800.00,
    'Gramo',
    '2026-08-20',
    'Cámara Fría B2',
    'Activo',
    'Semilla limpia procesada en laboratorio'
)
on conflict (id) do nothing;

-- ─────────────────────────────────────────────────────────────
-- Movimientos de Inventario (F4 / F5 / F8 / F9)
-- REVISIT: Esquema para movimiento_inventario y linea_movimiento.
-- Diseñado con campos directos y legibles para permitir evolución fácil
-- con F3 (Lab), F6 (Clientes: búsqueda multivariable) y F9 (Uso Interno: depto y solicitante).
-- ─────────────────────────────────────────────────────────────

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
"""
            use cmd = new NpgsqlCommand(sqlAuth, conn)
            cmd.ExecuteNonQuery() |> ignore
            printfn "[DbConnection] Base de datos e inicialización Auth/Productos/Lotes/Inventario (UUID v7) completadas exitosamente."
        with ex ->
            printfn "[DbConnection] Advertencia al inicializar BD: %s" ex.Message



namespace Sinbas.Infrastructure

open System
open Npgsql
open Dapper.FSharp.PostgreSQL

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

            use conn = crear ()
            conn.Open()

            let sqlAuth = """
create table if not exists empleado (
    id              uuid primary key,
    nombre_completo text    not null,
    estado          text    not null default 'Activo'
);

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

insert into empleado (id, nombre_completo, estado)
values ('01917f3a-0001-7000-8000-000000000001', 'Administrador del Sistema', 'Activo')
on conflict (id) do nothing;

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
"""
            use cmd = new NpgsqlCommand(sqlAuth, conn)
            cmd.ExecuteNonQuery() |> ignore
            printfn "[DbConnection] Base de datos e inicialización Auth/Productos (UUID v7) completadas exitosamente."
        with ex ->
            printfn "[DbConnection] Advertencia al inicializar BD: %s" ex.Message



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
"""
            use cmd = new NpgsqlCommand(sqlAuth, conn)
            cmd.ExecuteNonQuery() |> ignore
            printfn "[DbConnection] Base de datos e inicialización Auth/Seed (UUID v7) completadas exitosamente."
        with ex ->
            printfn "[DbConnection] Advertencia al inicializar BD: %s" ex.Message


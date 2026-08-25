namespace Sinbas.Infrastructure

open System
open Npgsql

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
            use conn = crear ()
            conn.Open()

            let sqlAuth = """
create table if not exists empleado (
    id              integer generated always as identity primary key,
    nombre_completo text    not null,
    estado          text    not null default 'Activo'
);

create table if not exists usuario (
    id              integer generated always as identity primary key,
    empleado_id     integer not null references empleado(id),
    nombre_usuario  text    not null unique,
    password_hash   text    not null,
    estado          text    not null default 'Activo'
);

create index if not exists ix_usuario_nombre
    on usuario(nombre_usuario);

create table if not exists usuario_rol (
    usuario_id  integer not null references usuario(id) on delete cascade,
    rol         text    not null,
    primary key (usuario_id, rol)
);

insert into empleado (nombre_completo, estado)
select 'Administrador del Sistema', 'Activo'
where not exists (select 1 from empleado where nombre_completo = 'Administrador del Sistema');

insert into usuario (empleado_id, nombre_usuario, password_hash, estado)
select
    (select id from empleado where nombre_completo = 'Administrador del Sistema' limit 1),
    'admin',
    '$2a$11$8bv8pfyb92XqSnbzqIP9vuvXcnfCotYVp6Svj6ASNbYJTftltLBmu',
    'Activo'
where not exists (select 1 from usuario where nombre_usuario = 'admin');

insert into usuario_rol (usuario_id, rol)
select
    (select id from usuario where nombre_usuario = 'admin' limit 1),
    'Administrador'
where not exists (
    select 1 from usuario_rol ur
    join usuario u on ur.usuario_id = u.id
    where u.nombre_usuario = 'admin' and ur.rol = 'Administrador'
);
"""
            use cmd = new NpgsqlCommand(sqlAuth, conn)
            cmd.ExecuteNonQuery() |> ignore
            printfn "[DbConnection] Base de datos e inicialización Auth/Seed completadas exitosamente."
        with ex ->
            printfn "[DbConnection] Advertencia al inicializar BD: %s" ex.Message


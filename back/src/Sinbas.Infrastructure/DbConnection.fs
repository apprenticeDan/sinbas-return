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

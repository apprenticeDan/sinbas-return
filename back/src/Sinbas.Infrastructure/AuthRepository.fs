namespace Sinbas.Infrastructure

open System
open Dapper
open Dapper.FSharp.PostgreSQL
open Sinbas.Domain
open Sinbas.Application

// ─────────────────────────────────────────────────────────────
// Tipos de fila para mapeo con Dapper
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type UsuarioRow =
    { id            : Guid
      empleado_id   : Guid
      nombre_usuario: string
      password_hash : string
      estado        : string }

[<CLIMutable>]
type UsuarioRolRow =
    { usuario_id: Guid
      rol       : string }

[<CLIMutable>]
type EmpleadoRow =
    { id              : Guid
      nombres         : string option
      apellido_paterno: string option
      apellido_materno: string option
      ci_numero       : string option
      ci_complemento  : string option
      telefono        : string option
      email           : string option
      nombre_completo : string
      estado          : string }

// ─────────────────────────────────────────────────────────────
// Def de Tablas para Dapper.FSharp
// ─────────────────────────────────────────────────────────────

module private AuthTables =
    let usuarioTable = table'<UsuarioRow> "usuario"
    let usuarioRolTable = table'<UsuarioRolRow> "usuario_rol"
    let empleadoTable = table'<EmpleadoRow> "empleado"

// ─────────────────────────────────────────────────────────────
// Helpers privados
// ─────────────────────────────────────────────────────────────

module private AuthRepositoryHelpers =

    let reconstruirEmpleado (row: EmpleadoRow) : Empleado =
        let nombres = defaultArg row.nombres (if String.IsNullOrWhiteSpace row.nombre_completo then "Usuario" else row.nombre_completo)
        let ci =
            match row.ci_numero with
            | Some n when not (String.IsNullOrWhiteSpace n) ->
                match CI.crear n row.ci_complemento with
                | Ok c -> c
                | Error _ -> { Numero = n; Complemento = row.ci_complemento }
            | _ -> { Numero = "-"; Complemento = None }

        let estado =
            match row.estado with
            | "Inactivo" -> EstadoEmpleado.Inactivo
            | _ -> EstadoEmpleado.Activo

        Empleado.reconstruir
            row.id
            nombres
            row.apellido_paterno
            row.apellido_materno
            ci
            row.telefono
            row.email
            estado

    let reconstruirDesdeFilas (row: UsuarioRow) (roles: UsuarioRolRow seq) : Usuario =
        let rolSet =
            roles
            |> Seq.choose (fun r ->
                match NombreRol.fromString r.rol with
                | Ok rol -> Some rol
                | Error _ -> None)
            |> Set.ofSeq

        let estado =
            match row.estado with
            | "Activo" -> EstadoUsuario.Activo
            | _        -> EstadoUsuario.Bloqueado

        Usuario.reconstruir
            row.id
            row.empleado_id
            row.nombre_usuario
            row.password_hash
            rolSet
            estado

    let cargarRoles (conn: Npgsql.NpgsqlConnection) (usuarioId: Guid) =
        async {
            let! roles =
                select {
                    for r in AuthTables.usuarioRolTable do
                    where (r.usuario_id = usuarioId)
                }
                |> conn.SelectAsync<UsuarioRolRow>
                |> Async.AwaitTask
            return roles
        }

    let persistirRoles (conn: Npgsql.NpgsqlConnection) (usuarioId: Guid) (roles: NombreRol Set) =
        async {
            do! delete {
                    for r in AuthTables.usuarioRolTable do
                    where (r.usuario_id = usuarioId)
                }
                |> conn.DeleteAsync
                |> Async.AwaitTask
                |> Async.Ignore

            let rolRows =
                roles
                |> Set.toList
                |> List.map (fun r -> { usuario_id = usuarioId; rol = NombreRol.toString r })

            if not rolRows.IsEmpty then
                do! insert {
                        into AuthTables.usuarioRolTable
                        values rolRows
                    }
                    |> conn.InsertAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
        }

// ─────────────────────────────────────────────────────────────
// Módulo público del repositorio
// ─────────────────────────────────────────────────────────────

module AuthRepository =

    open AuthRepositoryHelpers

    let buscarUsuarioPorNombre (nombre: NombreUsuario) : Async<Result<Usuario, AuthError>> =
        async {
            use conn = DbConnection.crear ()
            let valorNombre = NombreUsuario.valor nombre

            let! rows =
                select {
                    for u in AuthTables.usuarioTable do
                    where (u.nombre_usuario = valorNombre)
                }
                |> conn.SelectAsync<UsuarioRow>
                |> Async.AwaitTask

            match Seq.tryHead rows with
            | None -> return Error CredencialesInvalidas
            | Some row ->
                let! rolesSeq = cargarRoles conn row.id
                return Ok (reconstruirDesdeFilas row rolesSeq)
        }

    let buscarUsuarioPorId (id: UsuarioId) : Async<Result<Usuario, AuthError>> =
        async {
            use conn = DbConnection.crear ()
            let (UsuarioId rawId) = id

            let! rows =
                select {
                    for u in AuthTables.usuarioTable do
                    where (u.id = rawId)
                }
                |> conn.SelectAsync<UsuarioRow>
                |> Async.AwaitTask

            match Seq.tryHead rows with
            | None -> return Error (ErrorInterno "Usuario no encontrado")
            | Some row ->
                let! rolesSeq = cargarRoles conn row.id
                return Ok (reconstruirDesdeFilas row rolesSeq)
        }

    let listarUsuarios () : Async<Usuario list> =
        async {
            use conn = DbConnection.crear ()

            let! filas =
                select {
                    for u in AuthTables.usuarioTable do
                    orderBy u.nombre_usuario
                }
                |> conn.SelectAsync<UsuarioRow>
                |> Async.AwaitTask

            let! roles =
                select {
                    for r in AuthTables.usuarioRolTable do
                    selectAll
                }
                |> conn.SelectAsync<UsuarioRolRow>
                |> Async.AwaitTask

            let rolesPorUsuario =
                roles
                |> Seq.groupBy (fun r -> r.usuario_id)
                |> Map.ofSeq

            return
                filas
                |> Seq.map (fun row ->
                    let rolesDelUsuario =
                        rolesPorUsuario
                        |> Map.tryFind row.id
                        |> Option.defaultValue Seq.empty
                    reconstruirDesdeFilas row rolesDelUsuario)
                |> Seq.toList
        }

    let buscarEmpleadoPorId (id: EmpleadoId) : Async<Empleado option> =
        async {
            use conn = DbConnection.crear ()
            let (EmpleadoId rawId) = id
            let! rows =
                select {
                    for e in AuthTables.empleadoTable do
                    where (e.id = rawId)
                }
                |> conn.SelectAsync<EmpleadoRow>
                |> Async.AwaitTask
            return rows |> Seq.tryHead |> Option.map reconstruirEmpleado
        }

    let buscarUsuarioConEmpleadoPorId (id: UsuarioId) : Async<Result<UsuarioConEmpleado, AuthError>> =
        async {
            let! uRes = buscarUsuarioPorId id
            match uRes with
            | Error e -> return Error e
            | Ok u ->
                let! empOpt = buscarEmpleadoPorId (Usuario.empleadoId u)
                let (EmpleadoId eid) = Usuario.empleadoId u
                let emp =
                    empOpt
                    |> Option.defaultWith (fun () ->
                        { Id = EmpleadoId eid
                          Nombres = "Usuario"
                          ApellidoPaterno = None
                          ApellidoMaterno = None
                          CI = { Numero = "-"; Complemento = None }
                          Telefono = None
                          Email = None
                          Estado = EstadoEmpleado.Activo })
                return Ok { Usuario = u; Empleado = emp }
        }

    let listarUsuariosConEmpleado () : Async<UsuarioConEmpleado list> =
        async {
            use conn = DbConnection.crear ()

            let! filas =
                select {
                    for u in AuthTables.usuarioTable do
                    orderBy u.nombre_usuario
                }
                |> conn.SelectAsync<UsuarioRow>
                |> Async.AwaitTask

            let! empleados =
                select {
                    for e in AuthTables.empleadoTable do
                    selectAll
                }
                |> conn.SelectAsync<EmpleadoRow>
                |> Async.AwaitTask

            let empleadosPorId =
                empleados
                |> Seq.map (fun e -> e.id, reconstruirEmpleado e)
                |> Map.ofSeq

            let! roles =
                select {
                    for r in AuthTables.usuarioRolTable do
                    selectAll
                }
                |> conn.SelectAsync<UsuarioRolRow>
                |> Async.AwaitTask

            let rolesPorUsuario =
                roles
                |> Seq.groupBy (fun r -> r.usuario_id)
                |> Map.ofSeq

            return
                filas
                |> Seq.map (fun row ->
                    let rolesDelUsuario =
                        rolesPorUsuario
                        |> Map.tryFind row.id
                        |> Option.defaultValue Seq.empty
                    let usuario = reconstruirDesdeFilas row rolesDelUsuario
                    let empleado =
                        empleadosPorId
                        |> Map.tryFind row.empleado_id
                        |> Option.defaultWith (fun () ->
                            { Id = EmpleadoId row.empleado_id
                              Nombres = "Usuario"
                              ApellidoPaterno = None
                              ApellidoMaterno = None
                              CI = { Numero = "-"; Complemento = None }
                              Telefono = None
                              Email = None
                              Estado = EstadoEmpleado.Activo })
                    { Usuario = usuario; Empleado = empleado })
                |> Seq.toList
        }

    let guardarUsuarioYEmpleado (usuario: Usuario) (empleado: Empleado) : Async<Result<unit, AuthError>> =
        async {
            use conn = DbConnection.crear ()
            let (UsuarioId id)     = Usuario.id usuario
            let (EmpleadoId empId) = Usuario.empleadoId usuario
            let nombre             = Usuario.nombreUsuario usuario |> NombreUsuario.valor
            let (PasswordHash hash)= Usuario.hash usuario
            let estadoUsuario      = sprintf "%A" (Usuario.estado usuario)
            let roles              = Usuario.roles usuario

            let empRow =
                { id = empId
                  nombres = Some empleado.Nombres
                  apellido_paterno = empleado.ApellidoPaterno
                  apellido_materno = empleado.ApellidoMaterno
                  ci_numero = Some empleado.CI.Numero
                  ci_complemento = empleado.CI.Complemento
                  telefono = empleado.Telefono
                  email = empleado.Email
                  nombre_completo = empleado.NombreCompleto
                  estado = sprintf "%A" empleado.Estado }

            try
                // 1. Guardar o actualizar empleado
                let! empExistente =
                    select {
                        for e in AuthTables.empleadoTable do
                        where (e.id = empId)
                    }
                    |> conn.SelectAsync<EmpleadoRow>
                    |> Async.AwaitTask

                if Seq.isEmpty empExistente then
                    do! insert {
                            into AuthTables.empleadoTable
                            value empRow
                        }
                        |> conn.InsertAsync
                        |> Async.AwaitTask
                        |> Async.Ignore
                else
                    do! update {
                            for e in AuthTables.empleadoTable do
                            set empRow
                            where (e.id = empId)
                        }
                        |> conn.UpdateAsync
                        |> Async.AwaitTask
                        |> Async.Ignore

                // 2. Guardar o actualizar usuario
                let! usuarioExistente =
                    select {
                        for u in AuthTables.usuarioTable do
                        where (u.id = id)
                    }
                    |> conn.SelectAsync<UsuarioRow>
                    |> Async.AwaitTask

                let uRow =
                    { id = id
                      empleado_id = empId
                      nombre_usuario = nombre
                      password_hash = hash
                      estado = estadoUsuario }

                if Seq.isEmpty usuarioExistente then
                    do! insert {
                            into AuthTables.usuarioTable
                            value uRow
                        }
                        |> conn.InsertAsync
                        |> Async.AwaitTask
                        |> Async.Ignore
                else
                    do! update {
                            for u in AuthTables.usuarioTable do
                            set uRow
                            where (u.id = id)
                        }
                        |> conn.UpdateAsync
                        |> Async.AwaitTask
                        |> Async.Ignore

                // 3. Persistir roles
                do! persistirRoles conn id roles
                return Ok ()
            with ex ->
                return Error (ErrorInterno ex.Message)
        }

    let guardarUsuario (usuario: Usuario) : Async<Result<unit, AuthError>> =
        async {
            use conn = DbConnection.crear ()
            let (UsuarioId id)       = Usuario.id usuario
            let (EmpleadoId empId)   = Usuario.empleadoId usuario
            let nombre               = Usuario.nombreUsuario usuario |> NombreUsuario.valor
            let (PasswordHash hash)  = Usuario.hash usuario
            let estado               = sprintf "%A" (Usuario.estado usuario)
            let roles                = Usuario.roles usuario

            try
                // 1. Asegurar que el registro de empleado existe
                let! empleadoExistente =
                    select {
                        for e in AuthTables.empleadoTable do
                        where (e.id = empId)
                    }
                    |> conn.SelectAsync<EmpleadoRow>
                    |> Async.AwaitTask

                if Seq.isEmpty empleadoExistente then
                    let nuevoEmpleado =
                        { id = empId
                          nombres = Some (sprintf "Empleado #%s" (empId.ToString().Substring(0, 8)))
                          apellido_paterno = None
                          apellido_materno = None
                          ci_numero = Some "-"
                          ci_complemento = None
                          telefono = None
                          email = None
                          nombre_completo = sprintf "Empleado #%s" (empId.ToString().Substring(0, 8))
                          estado = "Activo" }
                    do! insert {
                            into AuthTables.empleadoTable
                            value nuevoEmpleado
                        }
                        |> conn.InsertAsync
                        |> Async.AwaitTask
                        |> Async.Ignore

                // 2. Verificar si el usuario ya existe
                let! usuarioExistente =
                    select {
                        for u in AuthTables.usuarioTable do
                        where (u.id = id)
                    }
                    |> conn.SelectAsync<UsuarioRow>
                    |> Async.AwaitTask

                let row =
                    { id = id
                      empleado_id = empId
                      nombre_usuario = nombre
                      password_hash = hash
                      estado = estado }

                if Seq.isEmpty usuarioExistente then
                    // INSERT con Dapper.FSharp
                    do! insert {
                            into AuthTables.usuarioTable
                            value row
                        }
                        |> conn.InsertAsync
                        |> Async.AwaitTask
                        |> Async.Ignore
                else
                    // UPDATE con Dapper.FSharp
                    do! update {
                            for u in AuthTables.usuarioTable do
                            set row
                            where (u.id = id)
                        }
                        |> conn.UpdateAsync
                        |> Async.AwaitTask
                        |> Async.Ignore

                do! persistirRoles conn id roles
                return Ok ()

            with ex ->
                return Error (ErrorInterno ex.Message)
        }

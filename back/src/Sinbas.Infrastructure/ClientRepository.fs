namespace Sinbas.Infrastructure

open System
open Dapper
open Dapper.FSharp.PostgreSQL
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Feature F6: Gestión de Clientes (RF08 | CU-17 | RN18)
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type ClienteRow =
    { id: Guid
      tipo: string
      nombres: string option
      apellido_paterno: string option
      apellido_materno: string option
      ci_numero: string option
      ci_complemento: string option
      ci_extension: string option
      razon_social: string option
      nit: string option
      rep_nombres: string option
      rep_apellido_paterno: string option
      rep_apellido_materno: string option
      rep_ci_numero: string option
      rep_ci_complemento: string option
      rep_ci_extension: string option
      rep_telefono: string option
      rep_email: string option
      telefono: string option
      email: string option
      direccion: string option
      estado: string }

module private ClientTables =
    let clienteTable = table'<ClienteRow> "cliente"

module ClientRepository =

    let private rowFromCliente (c: Cliente) : ClienteRow =
        let (ClienteId cid) = c.Id
        let estadoStr = EstadoCliente.aTexto c.Estado
        match c.Tipo with
        | TipoCliente.Natural p ->
            { id = cid
              tipo = "Natural"
              nombres = Some p.Nombres
              apellido_paterno = p.ApellidoPaterno
              apellido_materno = p.ApellidoMaterno
              ci_numero = Some p.CI.Numero
              ci_complemento = p.CI.Complemento
              ci_extension = p.CI.Extension |> Option.map DepartamentoExpedicion.aTexto
              razon_social = None
              nit = None
              rep_nombres = None
              rep_apellido_paterno = None
              rep_apellido_materno = None
              rep_ci_numero = None
              rep_ci_complemento = None
              rep_ci_extension = None
              rep_telefono = None
              rep_email = None
              telefono = c.Telefono
              email = c.Email
              direccion = c.Direccion
              estado = estadoStr }
        | TipoCliente.Juridica (rs, nit, repOpt) ->
            let repFields =
                match repOpt with
                | None -> (None, None, None, None, None, None, None, None)
                | Some r ->
                    (Some r.Nombres,
                     r.ApellidoPaterno,
                     r.ApellidoMaterno,
                     Some r.CI.Numero,
                     r.CI.Complemento,
                     r.CI.Extension |> Option.map DepartamentoExpedicion.aTexto,
                     r.Telefono,
                     r.Email)
            let (rNom, rPat, rMat, rCi, rComp, rExt, rTel, rEmail) = repFields
            { id = cid
              tipo = "Juridica"
              nombres = None
              apellido_paterno = None
              apellido_materno = None
              ci_numero = None
              ci_complemento = None
              ci_extension = None
              razon_social = Some (RazonSocial.valor rs)
              nit = Some (NIT.valor nit)
              rep_nombres = rNom
              rep_apellido_paterno = rPat
              rep_apellido_materno = rMat
              rep_ci_numero = rCi
              rep_ci_complemento = rComp
              rep_ci_extension = rExt
              rep_telefono = rTel
              rep_email = rEmail
              telefono = c.Telefono
              email = c.Email
              direccion = c.Direccion
              estado = estadoStr }

    let private clienteFromRow (row: ClienteRow) : Cliente =
        let estado =
            match EstadoCliente.desdeTexto row.estado with
            | Ok e -> e
            | Error _ -> EstadoCliente.Activo

        let tipo =
            match row.tipo with
            | "Juridica" ->
                let rs =
                    row.razon_social
                    |> Option.defaultValue "Empresa Sin Nombre"
                    |> RazonSocial.reconstruir
                let nit =
                    row.nit
                    |> Option.defaultValue "0"
                    |> NIT.reconstruir

                let repOpt =
                    match row.rep_nombres with
                    | Some n when not (String.IsNullOrWhiteSpace n) ->
                        let ext = row.rep_ci_extension |> Option.bind DepartamentoExpedicion.desdeTexto
                        let ci =
                            { Numero = defaultArg row.rep_ci_numero "0"
                              Complemento = row.rep_ci_complemento
                              Extension = ext }
                        Some (Persona.reconstruir (Identidad.nuevo ()) n row.rep_apellido_paterno row.rep_apellido_materno ci row.rep_telefono row.rep_email)
                    | _ -> None

                TipoCliente.Juridica (rs, nit, repOpt)
            | _ -> // Natural
                let ext = row.ci_extension |> Option.bind DepartamentoExpedicion.desdeTexto
                let ci =
                    { Numero = defaultArg row.ci_numero "0"
                      Complemento = row.ci_complemento
                      Extension = ext }
                let nombres = defaultArg row.nombres "Cliente"
                let persona = Persona.reconstruir (Identidad.nuevo ()) nombres row.apellido_paterno row.apellido_materno ci row.telefono row.email
                TipoCliente.Natural persona

        Cliente.reconstruir row.id tipo row.telefono row.email row.direccion estado

    let insertar (cliente: Cliente) : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            let row = rowFromCliente cliente
            try
                do! insert {
                        into ClientTables.clienteTable
                        value row
                    }
                    |> conn.InsertAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                return Ok ()
            with ex ->
                return Error ex.Message
        }

    let obtenerPorId (id: ClienteId) : Async<Cliente option> =
        async {
            use conn = DbConnection.crear ()
            let (ClienteId cid) = id
            let! rows =
                select {
                    for c in ClientTables.clienteTable do
                    where (c.id = cid)
                }
                |> conn.SelectAsync<ClienteRow>
                |> Async.AwaitTask

            return rows |> Seq.tryHead |> Option.map clienteFromRow
        }

    let actualizar (cliente: Cliente) : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            let (ClienteId cid) = cliente.Id
            let row = rowFromCliente cliente
            try
                do! update {
                        for c in ClientTables.clienteTable do
                        set row
                        where (c.id = cid)
                    }
                    |> conn.UpdateAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
                return Ok ()
            with ex ->
                return Error ex.Message
        }


    let listarTodos () : Async<Cliente list> =
        async {
            use conn = DbConnection.crear ()
            let sql = "SELECT * FROM cliente ORDER BY id DESC"
            let! rows = conn.QueryAsync<ClienteRow>(sql) |> Async.AwaitTask
            return rows |> Seq.map clienteFromRow |> Seq.toList
        }

    let buscarPorTermino (termino: string) : Async<Cliente list> =
        async {
            use conn = DbConnection.crear ()
            if String.IsNullOrWhiteSpace termino then
                let sql = "SELECT * FROM cliente ORDER BY id DESC"
                let! rows = conn.QueryAsync<ClienteRow>(sql) |> Async.AwaitTask
                return rows |> Seq.map clienteFromRow |> Seq.toList
            else
                let sql = """
                    SELECT * FROM cliente
                    WHERE LOWER(COALESCE(nombres, '')) LIKE @q
                       OR LOWER(COALESCE(apellido_paterno, '')) LIKE @q
                       OR LOWER(COALESCE(apellido_materno, '')) LIKE @q
                       OR LOWER(COALESCE(razon_social, '')) LIKE @q
                       OR LOWER(COALESCE(nit, '')) LIKE @q
                       OR LOWER(COALESCE(ci_numero, '')) LIKE @q
                    ORDER BY id DESC
                """
                let param = {| q = "%" + termino.Trim().ToLowerInvariant() + "%" |}
                let! rows = conn.QueryAsync<ClienteRow>(sql, param) |> Async.AwaitTask
                return rows |> Seq.map clienteFromRow |> Seq.toList
        }

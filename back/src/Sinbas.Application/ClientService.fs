namespace Sinbas.Application

open System
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Feature F6: Gestión de Clientes (RF08 | CU-17 | RN18)
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type PersonaDto =
    { Nombres: string
      ApellidoPaterno: string option
      ApellidoMaterno: string option
      NombreCompleto: string
      CiNumero: string
      CiComplemento: string option
      CiExtension: string option
      CiFormateado: string
      Telefono: string option
      Email: string option }

[<CLIMutable>]
type ClienteDto =
    { Id: string
      Tipo: string // "Natural" | "Juridica"
      NombreVisible: string
      // Persona Natural
      Persona: PersonaDto option
      // Persona Jurídica
      RazonSocial: string option
      Nit: string option
      Representante: PersonaDto option
      // Contacto y estado general
      Telefono: string option
      Email: string option
      Direccion: string option
      Estado: string } // "Activo" | "Inactivo"

[<CLIMutable>]
type CrearClienteNaturalRequest =
    { Nombres: string
      ApellidoPaterno: string option
      ApellidoMaterno: string option
      CiNumero: string
      CiComplemento: string option
      CiExtension: string option
      Telefono: string option
      Email: string option
      Direccion: string option }

[<CLIMutable>]
type RepresentanteRequest =
    { Nombres: string
      ApellidoPaterno: string option
      ApellidoMaterno: string option
      CiNumero: string
      CiComplemento: string option
      CiExtension: string option
      Telefono: string option
      Email: string option }

[<CLIMutable>]
type CrearClienteJuridicaRequest =
    { RazonSocial: string
      Nit: string
      Representante: RepresentanteRequest option
      Telefono: string option
      Email: string option
      Direccion: string option }

[<CLIMutable>]
type ActualizarClienteRequest =
    { Tipo: string option
      Nombres: string option
      ApellidoPaterno: string option
      ApellidoMaterno: string option
      CiNumero: string option
      CiComplemento: string option
      CiExtension: string option
      RazonSocial: string option
      Nit: string option
      Representante: RepresentanteRequest option
      Telefono: string option
      Email: string option
      Direccion: string option
      Estado: string option }


module ClientService =

    let private errorToString (err: DomainError) : string =
        match err with
        | CantidadInvalida msg
        | UnidadIncompatible msg
        | CodigoLoteInvalido msg
        | NombreInvalido msg
        | CIInvalido msg
        | ValorRequerido msg
        | SecuenciaInvalida msg
        | StockInsuficiente msg
        | SimbolosNoPermitidos msg
        | LetrasNoPermitidas msg
        | EmpleadoDuplicado msg
        | SinAnalisisLaboratorio msg
        | LoteRechazado msg
        | PorcentajeInvalido msg
        | FechaInvalida msg -> msg

    let toPersonaDto (p: Persona) : PersonaDto =
        let extStr = p.CI.Extension |> Option.map DepartamentoExpedicion.aTexto
        { Nombres = p.Nombres
          ApellidoPaterno = p.ApellidoPaterno
          ApellidoMaterno = p.ApellidoMaterno
          NombreCompleto = p.NombreCompleto
          CiNumero = p.CI.Numero
          CiComplemento = p.CI.Complemento
          CiExtension = extStr
          CiFormateado = CI.formatear p.CI
          Telefono = p.Telefono
          Email = p.Email }

    let toDto (c: Cliente) : ClienteDto =
        let (ClienteId id) = c.Id
        let tipoStr = TipoCliente.aTexto c.Tipo
        let nomVis = Cliente.nombreVisible c
        let estadoStr = EstadoCliente.aTexto c.Estado
        match c.Tipo with
        | TipoCliente.Natural persona ->
            { Id = id.ToString()
              Tipo = tipoStr
              NombreVisible = nomVis
              Persona = Some (toPersonaDto persona)
              RazonSocial = None
              Nit = None
              Representante = None
              Telefono = c.Telefono
              Email = c.Email
              Direccion = c.Direccion
              Estado = estadoStr }
        | TipoCliente.Juridica (rs, nit, repOpt) ->
            { Id = id.ToString()
              Tipo = tipoStr
              NombreVisible = nomVis
              Persona = None
              RazonSocial = Some (RazonSocial.valor rs)
              Nit = Some (NIT.valor nit)
              Representante = repOpt |> Option.map toPersonaDto
              Telefono = c.Telefono
              Email = c.Email
              Direccion = c.Direccion
              Estado = estadoStr }

    let private crearPersonaDesdeDatos
        (nombres: string)
        (pat: string option)
        (mat: string option)
        (ciNum: string)
        (ciComp: string option)
        (ciExt: string option)
        (tel: string option)
        (email: string option)
        : Result<Persona, string> =
        let extOpt = ciExt |> Option.bind DepartamentoExpedicion.desdeTexto
        match CI.crear ciNum ciComp extOpt with
        | Error err -> Error (errorToString err)
        | Ok ci ->
            match Persona.crear nombres pat mat ci tel email with
            | Error err -> Error (errorToString err)
            | Ok persona -> Ok persona

    /// Registrar nuevo cliente Persona Natural (RF08 / CU-17 / RN18)
    let crearClienteNatural
        (insertarCliente: Cliente -> Async<Result<unit, string>>)
        (req: CrearClienteNaturalRequest)
        : Async<Result<ClienteDto, string>> =
        async {
            match crearPersonaDesdeDatos req.Nombres req.ApellidoPaterno req.ApellidoMaterno req.CiNumero req.CiComplemento req.CiExtension req.Telefono req.Email with
            | Error msg -> return Error msg
            | Ok persona ->
                match Cliente.crearNatural persona req.Telefono req.Email req.Direccion with
                | Error err -> return Error (errorToString err)
                | Ok cliente ->
                    let! insertRes = insertarCliente cliente
                    match insertRes with
                    | Ok () -> return Ok (toDto cliente)
                    | Error msg -> return Error msg
        }

    /// Registrar nuevo cliente Persona Jurídica (RF08 / CU-17 / RN18)
    let crearClienteJuridica
        (insertarCliente: Cliente -> Async<Result<unit, string>>)
        (req: CrearClienteJuridicaRequest)
        : Async<Result<ClienteDto, string>> =
        async {
            let resRs = RazonSocial.crear req.RazonSocial
            let resNit = NIT.crear req.Nit
            match resRs, resNit with
            | Error err, _ -> return Error (errorToString err)
            | _, Error err -> return Error (errorToString err)
            | Ok rs, Ok nit ->
                let repRes =
                    match req.Representante with
                    | None -> Ok None
                    | Some r ->
                        match crearPersonaDesdeDatos r.Nombres r.ApellidoPaterno r.ApellidoMaterno r.CiNumero r.CiComplemento r.CiExtension r.Telefono r.Email with
                        | Ok p -> Ok (Some p)
                        | Error msg -> Error msg

                match repRes with
                | Error msg -> return Error msg
                | Ok repOpt ->
                    match Cliente.crearJuridica rs nit repOpt req.Telefono req.Email req.Direccion with
                    | Error err -> return Error (errorToString err)
                    | Ok cliente ->
                        let! insertRes = insertarCliente cliente
                        match insertRes with
                        | Ok () -> return Ok (toDto cliente)
                        | Error msg -> return Error msg
        }

    /// Obtener cliente por ID
    let obtenerClientePorId
        (obtenerPorId: ClienteId -> Async<Cliente option>)
        (idRaw: string)
        : Async<Result<ClienteDto, string>> =
        async {
            match Guid.TryParse idRaw with
            | false, _ -> return Error (sprintf "El ID de cliente '%s' no tiene un formato válido" idRaw)
            | true, guid ->
                let! cOpt = obtenerPorId (ClienteId guid)
                match cOpt with
                | None -> return Error (sprintf "No se encontró el cliente con ID '%s'" idRaw)
                | Some c -> return Ok (toDto c)
        }

    /// Listar todos los clientes
    let listarClientes
        (listarEnRepo: unit -> Async<Cliente list>)
        ()
        : Async<ClienteDto list> =
        async {
            let! clientes = listarEnRepo ()
            return clientes |> List.map toDto
        }

    /// Buscar clientes por término de búsqueda (nombre, razón social, NIT o CI)
    let buscarClientes
        (buscarEnRepo: string -> Async<Cliente list>)
        (termino: string)
        : Async<ClienteDto list> =
        async {
            let! clientes = buscarEnRepo (if isNull termino then "" else termino.Trim())
            return clientes |> List.map toDto
        }

    /// Actualizar los datos de un cliente existente (RF08)
    let actualizarCliente
        (obtenerPorId: ClienteId -> Async<Cliente option>)
        (actualizarRepo: Cliente -> Async<Result<unit, string>>)
        (idRaw: string)
        (req: ActualizarClienteRequest)
        : Async<Result<ClienteDto, string>> =
        async {
            match Guid.TryParse idRaw with
            | false, _ -> return Error (sprintf "El ID de cliente '%s' no tiene un formato válido" idRaw)
            | true, guid ->
                let clienteId = ClienteId guid
                let! cOpt = obtenerPorId clienteId
                match cOpt with
                | None -> return Error (sprintf "No se encontró el cliente con ID '%s'" idRaw)
                | Some cActual ->
                    let nuevoEstado =
                        match req.Estado with
                        | Some s when not (String.IsNullOrWhiteSpace s) ->
                            match EstadoCliente.desdeTexto s with
                            | Ok est -> est
                            | Error _ -> cActual.Estado
                        | _ -> cActual.Estado

                    match cActual.Tipo with
                    | TipoCliente.Natural personaExistente ->
                        let nombres = req.Nombres |> Option.defaultValue personaExistente.Nombres
                        let pat = req.ApellidoPaterno |> Option.orElse personaExistente.ApellidoPaterno
                        let mat = req.ApellidoMaterno |> Option.orElse personaExistente.ApellidoMaterno
                        let ciNum = req.CiNumero |> Option.defaultValue personaExistente.CI.Numero
                        let ciComp = req.CiComplemento |> Option.orElse personaExistente.CI.Complemento
                        let ciExt =
                            match req.CiExtension with
                            | Some extStr -> Some extStr
                            | None -> personaExistente.CI.Extension |> Option.map DepartamentoExpedicion.aTexto
                        let telPers = req.Telefono |> Option.orElse personaExistente.Telefono
                        let emailPers = req.Email |> Option.orElse personaExistente.Email

                        match crearPersonaDesdeDatos nombres pat mat ciNum ciComp ciExt telPers emailPers with
                        | Error msg -> return Error msg
                        | Ok pNueva ->
                            // Mantener el PersonaId original (agregación funcional pura, sin herencia)
                            let personaActualizada = { pNueva with Id = personaExistente.Id }
                            let tel = req.Telefono |> Option.orElse cActual.Telefono
                            let email = req.Email |> Option.orElse cActual.Email
                            let dir = req.Direccion |> Option.orElse cActual.Direccion
                            match Cliente.actualizarNatural personaActualizada tel email dir nuevoEstado cActual with
                            | Error err -> return Error (errorToString err)
                            | Ok cActualizado ->
                                let! updateRes = actualizarRepo cActualizado
                                match updateRes with
                                | Ok () -> return Ok (toDto cActualizado)
                                | Error msg -> return Error msg

                    | TipoCliente.Juridica (rsExistente, nitExistente, repExistente) ->
                        let rsRes =
                            match req.RazonSocial with
                            | Some rsStr when not (String.IsNullOrWhiteSpace rsStr) -> RazonSocial.crear rsStr
                            | _ -> Ok rsExistente
                        let nitRes =
                            match req.Nit with
                            | Some nitStr when not (String.IsNullOrWhiteSpace nitStr) -> NIT.crear nitStr
                            | _ -> Ok nitExistente

                        match rsRes, nitRes with
                        | Error err, _ -> return Error (errorToString err)
                        | _, Error err -> return Error (errorToString err)
                        | Ok rs, Ok nit ->
                            let repRes =
                                match req.Representante with
                                | Some r ->
                                    match crearPersonaDesdeDatos r.Nombres r.ApellidoPaterno r.ApellidoMaterno r.CiNumero r.CiComplemento r.CiExtension r.Telefono r.Email with
                                    | Ok pNueva ->
                                        let repId = repExistente |> Option.map (fun rOld -> rOld.Id) |> Option.defaultValue (PersonaId (Identidad.nuevo ()))
                                        Ok (Some { pNueva with Id = repId })
                                    | Error msg -> Error msg
                                | None -> Ok repExistente

                            match repRes with
                            | Error msg -> return Error msg
                            | Ok repOpt ->
                                let tel = req.Telefono |> Option.orElse cActual.Telefono
                                let email = req.Email |> Option.orElse cActual.Email
                                let dir = req.Direccion |> Option.orElse cActual.Direccion
                                match Cliente.actualizarJuridica rs nit repOpt tel email dir nuevoEstado cActual with
                                | Error err -> return Error (errorToString err)
                                | Ok cActualizado ->
                                    let! updateRes = actualizarRepo cActualizado
                                    match updateRes with
                                    | Ok () -> return Ok (toDto cActualizado)
                                    | Error msg -> return Error msg
        }


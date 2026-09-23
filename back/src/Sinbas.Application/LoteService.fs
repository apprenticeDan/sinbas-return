namespace Sinbas.Application

open System
open Sinbas.Domain

[<CLIMutable>]
type CrearLoteRequest =
    { ProductoId: string
      CodigoPersonalizado: string
      Procedencia: string
      Cantidad: decimal
      Unidad: string
      FechaIngreso: string
      Ubicacion: string
      Observaciones: string }

[<CLIMutable>]
type LoteDto =
    { Id: string
      Codigo: string
      ProductoId: string
      NombreProducto: string
      Procedencia: string
      CantidadInicial: decimal
      CantidadActual: decimal
      Unidad: string
      FechaIngreso: string
      Ubicacion: string
      Estado: string
      Observaciones: string }

[<CLIMutable>]
type BloquearLoteRequest =
    { Motivo: string }

[<CLIMutable>]
type AnalisisResumenDto =
    { Id: string
      FechaAnalisis: string
      Germinacion: decimal
      Pureza: decimal
      Humedad: decimal
      Viabilidad: decimal
      Dictamen: string
      Observaciones: string option }

[<CLIMutable>]
type FichaTecnicaLoteDto =
    { Id: string
      Codigo: string
      ProductoId: string
      NombreProducto: string
      Categoria: string
      Genero: string option
      Epiteto: string option
      Procedencia: string option
      CantidadInicial: decimal
      CantidadActual: decimal
      Unidad: string
      FechaIngreso: string
      Ubicacion: string option
      Observaciones: string option
      Estado: string
      HistorialAnalisis: AnalisisResumenDto list
      UltimoDictamen: string option }


module LoteService =

    let private desmapearUnidad (uStr: string) : UnidadMedida =
        match (if isNull uStr then "" else uStr.Trim().ToLowerInvariant()) with
        | "gramo" | "g" -> Gramo
        | "kilogramo" | "kg" -> Kilogramo
        | "mililitro" | "ml" -> Mililitro
        | "litro" | "l" -> Litro
        | _ -> UnidadDiscreta

    let private aLoteDto (nombreProducto: string) (lote: Lote) : LoteDto =
        let (LoteId lId) = lote.Id
        let (ProductoId pId) = lote.ProductoId

        let estadoStr =
            match lote.Estado with
            | Activo -> "Activo"
            | Agotado -> "Agotado"
            | Bloqueado -> "Bloqueado"
            | Rechazado -> "Rechazado"
            | Archivado -> "Archivado"

        { Id = lId.ToString()
          Codigo = CodigoLote.valor lote.Codigo
          ProductoId = pId.ToString()
          NombreProducto = nombreProducto
          Procedencia = lote.Procedencia |> Option.defaultValue ""
          CantidadInicial = lote.CantidadInicial.Valor
          CantidadActual = lote.CantidadActual.Valor
          Unidad = Unidad.etiqueta lote.CantidadInicial.Unidad
          FechaIngreso = lote.FechaIngreso.ToString("yyyy-MM-dd")
          Ubicacion = lote.Ubicacion |> Option.defaultValue ""
          Estado = estadoStr
          Observaciones = lote.Observaciones |> Option.defaultValue "" }

    let registrarIngresoLote
        (getProducto: ProductoId -> Async<Producto option>)
        (insertarLote: Lote -> Async<unit>)
        (obtenerSecuenciaHoy: unit -> Async<int>)
        (req: CrearLoteRequest)
        : Async<Result<LoteDto, string>> =
        async {
            match Guid.TryParse(req.ProductoId) with
            | false, _ -> return Error "UUID de producto inválido"
            | true, prodGuid ->
                let prodId = ProductoId prodGuid
                let! optProd = getProducto prodId

                match optProd with
                | None -> return Error "El producto especificado no existe en el catálogo"
                | Some prod ->
                    let fecha =
                        match DateOnly.TryParse(req.FechaIngreso) with
                        | true, d -> d
                        | false, _ -> DateOnly.FromDateTime(DateTime.Today)

                    // Autogenerar código si no viene uno personalizado
                    let resCodigo =
                        if not (String.IsNullOrWhiteSpace(req.CodigoPersonalizado)) then
                            CodigoLote.desdeString req.CodigoPersonalizado
                        else
                            let (genero, epiteto) =
                                match prod.Categoria with
                                | Semilla(nc, _) -> (nc.Genero, nc.Epiteto)
                                | Plantin(nc, _, _) -> (nc.Genero, nc.Epiteto)
                                | Insumo(nombre, _, _) -> (nombre, "INS")

                            // Generar código estándar
                            CodigoLote.generar genero epiteto fecha 1

                    match resCodigo with
                    | Error err -> return Error(sprintf "Código de lote inválido: %A" err)
                    | Ok codigo ->
                        let unidad = desmapearUnidad req.Unidad
                        let cantRes = Cantidad.crear req.Cantidad unidad

                        match cantRes with
                        | Error err -> return Error(sprintf "Cantidad inválida: %A" err)
                        | Ok cantInicial ->
                            let newLoteId = LoteId(Identidad.nuevo ())
                            let procOpt = if String.IsNullOrWhiteSpace(req.Procedencia) then None else Some(req.Procedencia.Trim())
                            let ubiOpt = if String.IsNullOrWhiteSpace(req.Ubicacion) then None else Some(req.Ubicacion.Trim())
                            let obsOpt = if String.IsNullOrWhiteSpace(req.Observaciones) then None else Some(req.Observaciones.Trim())

                            match Lote.crear newLoteId codigo prodId procOpt cantInicial fecha ubiOpt obsOpt with
                            | Error err -> return Error(sprintf "Error de dominio al crear lote: %A" err)
                            | Ok nuevoLote ->
                                do! insertarLote nuevoLote
                                return Ok(aLoteDto (Producto.nombreVisible prod) nuevoLote)
        }

    let listarLotes
        (obtenerLotes: ProductoId option -> EstadoLote option -> Async<Lote list>)
        (obtenerProductos: unit -> Async<Producto list>)
        (productoIdFilter: string option)
        (estadoFilter: string option)
        : Async<LoteDto list> =
        async {
            let pidOpt =
                match productoIdFilter with
                | Some s when not (String.IsNullOrWhiteSpace(s)) ->
                    match Guid.TryParse(s) with
                    | true, g -> Some(ProductoId g)
                    | _ -> None
                | _ -> None

            let estOpt =
                match estadoFilter with
                | Some "Activo" -> Some Activo
                | Some "Agotado" -> Some Agotado
                | Some "Bloqueado" -> Some Bloqueado
                | Some "Archivado" -> Some Archivado
                | _ -> None

            let! lotes = obtenerLotes pidOpt estOpt
            let! productos = obtenerProductos ()

            let mapProductos =
                productos
                |> List.map (fun p ->
                    let (ProductoId pid) = p.Base.Id
                    (pid, Producto.nombreVisible p))
                |> Map.ofList

            let dtos =
                lotes
                |> List.map (fun l ->
                    let (ProductoId pid) = l.ProductoId
                    let nombreProd = mapProductos |> Map.tryFind pid |> Option.defaultValue "Producto Desconocido"
                    aLoteDto nombreProd l)

            return dtos
        }

    let bloquearLote
        (obtenerLote: LoteId -> Async<Lote option>)
        (obtenerProducto: ProductoId -> Async<Producto option>)
        (actualizarLote: Lote -> Async<unit>)
        (loteIdStr: string)
        (motivo: string)
        : Async<Result<LoteDto, string>> =
        async {
            match Guid.TryParse(loteIdStr) with
            | false, _ -> return Error "ID de lote inválido"
            | true, gId ->
                let loteId = LoteId gId
                let! optLote = obtenerLote loteId

                match optLote with
                | None -> return Error "Lote no encontrado"
                | Some lote ->
                    let loteBloqueado = Lote.bloquear motivo lote
                    do! actualizarLote loteBloqueado
                    let! optProd = obtenerProducto lote.ProductoId
                    let nomProd = optProd |> Option.map Producto.nombreVisible |> Option.defaultValue "Producto Desconocido"
                    return Ok(aLoteDto nomProd loteBloqueado)
        }

    /// Consultar ficha técnica completa de un lote (RF14 / CU-14 / F-LAB-06)
    let consultarFichaTecnicaLote
        (obtenerLote: LoteId -> Async<Lote option>)
        (obtenerProducto: ProductoId -> Async<Producto option>)
        (listarAnalisis: LoteId -> Async<AnalisisLaboratorio list>)
        (loteIdStr: string)
        : Async<Result<FichaTecnicaLoteDto, string>> =
        async {
            match Guid.TryParse(loteIdStr) with
            | false, _ -> return Error "ID de lote inválido"
            | true, gId ->
                let loteId = LoteId gId
                let! optLote = obtenerLote loteId

                match optLote with
                | None -> return Error (sprintf "No se encontró el lote con ID '%s'" loteIdStr)
                | Some lote ->
                    let! optProd = obtenerProducto lote.ProductoId
                    let! analisisList = listarAnalisis loteId

                    let (nombreProd, catStr, generoOpt, epitetoOpt) =
                        match optProd with
                        | None -> ("Producto Desconocido", "Desconocida", None, None)
                        | Some prod ->
                            let nom = Producto.nombreVisible prod
                            match prod.Categoria with
                            | Semilla(nc, _) -> (nom, "Semilla", Some nc.Genero, Some nc.Epiteto)
                            | Plantin(nc, _, _) -> (nom, "Plantin", Some nc.Genero, Some nc.Epiteto)
                            | Insumo(nombre, _, _) -> (nom, "Insumo", Some nombre, None)

                    let estadoStr =
                        match lote.Estado with
                        | Activo -> "Activo"
                        | Agotado -> "Agotado"
                        | Bloqueado -> "Bloqueado"
                        | Rechazado -> "Rechazado"
                        | Archivado -> "Archivado"

                    let analisisDtos =
                        analisisList
                        |> List.sortByDescending (fun a -> a.FechaAnalisis)
                        |> List.map (fun a ->
                            let (LaboratorioId aId) = a.Id
                            { Id = aId.ToString()
                              FechaAnalisis = a.FechaAnalisis.ToString("yyyy-MM-dd")
                              Germinacion = PorcentajeCalidad.valor a.Germinacion
                              Pureza = PorcentajeCalidad.valor a.Pureza
                              Humedad = PorcentajeCalidad.valor a.Humedad
                              Viabilidad = PorcentajeCalidad.valor a.Viabilidad
                              Dictamen = DictamenCalidad.aTexto a.Dictamen
                              Observaciones = a.Observaciones })

                    let ultimoDictamen =
                        analisisDtos
                        |> List.tryHead
                        |> Option.map (fun a -> a.Dictamen)

                    let (LoteId lId) = lote.Id
                    let (ProductoId pId) = lote.ProductoId

                    let dto : FichaTecnicaLoteDto =
                        { Id = lId.ToString()
                          Codigo = CodigoLote.valor lote.Codigo
                          ProductoId = pId.ToString()
                          NombreProducto = nombreProd
                          Categoria = catStr
                          Genero = generoOpt
                          Epiteto = epitetoOpt
                          Procedencia = lote.Procedencia
                          CantidadInicial = lote.CantidadInicial.Valor
                          CantidadActual = lote.CantidadActual.Valor
                          Unidad = Unidad.etiqueta lote.CantidadInicial.Unidad
                          FechaIngreso = lote.FechaIngreso.ToString("yyyy-MM-dd")
                          Ubicacion = lote.Ubicacion
                          Observaciones = lote.Observaciones
                          Estado = estadoStr
                          HistorialAnalisis = analisisDtos
                          UltimoDictamen = ultimoDictamen }

                    return Ok dto
        }


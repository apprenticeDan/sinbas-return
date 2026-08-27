namespace Sinbas.Application

open System
open Sinbas.Domain

type CrearProductoRequest =
    { Categoria: string
      Genero: string option
      Epiteto: string option
      ObservacionesNc: string option
      NombresComunes: string list
      EtapaDesarrollo: string option
      NombreInsumo: string option
      MarcaInsumo: string option
      DescripcionInsumo: string option
      UnidadManejo: string
      GramosNominales: decimal option
      Trazabilidad: string
      Observaciones: string option }

type AsignarPrecioRequest =
    { ProductoId: Guid
      Monto: decimal
      Moneda: string option }

type ProductoDto =
    { Id: Guid
      NombreVisible: string
      Categoria: string
      Genero: string option
      Epiteto: string option
      NombresComunes: string list
      UnidadManejo: string
      Trazabilidad: string
      PrecioOficial: decimal option
      Moneda: string option
      EstadoComercial: string
      Activo: bool
      EsAptoParaVenta: bool
      Observaciones: string option }

type GuardarProducto = Producto -> Async<Result<unit, string>>
type BuscarProductoPorId = ProductoId -> Async<Producto option>
type ListarProductos = unit -> Async<Producto list>

module CatalogService =

    let private mapUnidad (u: string) (g: decimal option) =
        match u with
        | "Gramo" -> Gramo
        | "Kilogramo" -> Kilogramo
        | "Bolsa" -> Bolsa (defaultArg g 1000m)
        | _ -> Unidad_

    let aDto (p: Producto) : ProductoDto =
        let (ProductoId rawId) = p.Base.Id
        let nc = Producto.nombreCientifico p
        let ncs = Producto.nombresComunes p |> List.map NombreComun.valor
        let catTexto =
            match p.Categoria with
            | Semilla _ -> "Semilla"
            | Plantin _ -> "Plantin"
            | Insumo _ -> "Insumo"
            | Otro _ -> "Otro"

        { Id = rawId
          NombreVisible = Producto.nombreVisible p
          Categoria = catTexto
          Genero = nc |> Option.map (fun x -> x.Genero)
          Epiteto = nc |> Option.map (fun x -> x.Epiteto)
          NombresComunes = ncs
          UnidadManejo =
            match p.Base.UnidadManejo with
            | Gramo -> "Gramo"
            | Kilogramo -> "Kilogramo"
            | Unidad_ -> "Unidad_"
            | Bolsa g -> sprintf "Bolsa(%g g)" (float g)
          Trazabilidad = (if p.Base.Trazabilidad = PorLote then "PorLote" else "Simple")
          PrecioOficial = p.PrecioOficial |> Option.map (fun x -> x.Valor)
          Moneda = p.PrecioOficial |> Option.map (fun x -> x.Moneda)
          EstadoComercial = EstadoComercial.aTexto p.EstadoComercial
          Activo = p.Base.Activo
          EsAptoParaVenta = Producto.esAptoParaVenta p
          Observaciones = p.Base.Observaciones }

    let crearProducto (guardar: GuardarProducto) (req: CrearProductoRequest) : Async<Result<ProductoDto, string>> =
        async {
            let unidad = mapUnidad req.UnidadManejo req.GramosNominales
            let trazabilidad = if req.Trazabilidad = "PorLote" then PorLote else Simple

            let resCat =
                match req.Categoria with
                | "Semilla" ->
                    match NombreCientifico.crear (defaultArg req.Genero "") (defaultArg req.Epiteto "") req.ObservacionesNc with
                    | Error (NombreInvalido msg) -> Error msg
                    | Error _ -> Error "Nombre científico inválido"
                    | Ok nc ->
                        let ncs = req.NombresComunes |> List.choose (fun n -> match NombreComun.crear n with Ok c -> Some c | Error _ -> None)
                        Ok (Semilla(nc, ncs))
                | "Plantin" ->
                    match NombreCientifico.crear (defaultArg req.Genero "") (defaultArg req.Epiteto "") req.ObservacionesNc with
                    | Error (NombreInvalido msg) -> Error msg
                    | Error _ -> Error "Nombre científico inválido"
                    | Ok nc ->
                        let ncs = req.NombresComunes |> List.choose (fun n -> match NombreComun.crear n with Ok c -> Some c | Error _ -> None)
                        Ok (Plantin(nc, ncs, req.EtapaDesarrollo))
                | "Insumo" ->
                    let nom = defaultArg req.NombreInsumo "Insumo sin nombre"
                    Ok (Insumo(nom, req.MarcaInsumo, req.DescripcionInsumo))
                | _ ->
                    let nom = defaultArg req.NombreInsumo "Producto general"
                    Ok (Otro(nom, req.DescripcionInsumo))

            match resCat with
            | Error err -> return Error err
            | Ok categoria ->
                let prodId = ProductoId (Identidad.nuevo ())
                let producto = Producto.crearBorrador prodId unidad trazabilidad categoria req.Observaciones
                
                let! resGuardar = guardar producto
                match resGuardar with
                | Ok () -> return Ok (aDto producto)
                | Error err -> return Error err
        }

    let asignarPrecio (buscarPorId: BuscarProductoPorId) (guardar: GuardarProducto) (req: AsignarPrecioRequest) (usuarioId: Guid) : Async<Result<ProductoDto, string>> =
        async {
            let prodId = ProductoId req.ProductoId
            let! optProd = buscarPorId prodId
            match optProd with
            | None -> return Error "Producto no encontrado"
            | Some prod ->
                let usrId = UsuarioId usuarioId
                match Producto.asignarPrecio req.Monto req.Moneda (Some usrId) prod with
                | Error (CantidadInvalida msg) -> return Error msg
                | Error _ -> return Error "Error al asignar precio"
                | Ok prodActualizado ->
                    let! resGuardar = guardar prodActualizado
                    match resGuardar with
                    | Ok () -> return Ok (aDto prodActualizado)
                    | Error err -> return Error err
        }

    let listarCatalogo (listar: ListarProductos) (categoriaFiltro: string option) (estadoFiltro: string option) : Async<ProductoDto list> =
        async {
            let! productos = listar ()
            let filtrados =
                productos
                |> List.filter (fun p ->
                    let matchCat =
                        match categoriaFiltro with
                        | None | Some "" -> true
                        | Some cat ->
                            match p.Categoria with
                            | Semilla _ -> cat = "Semilla"
                            | Plantin _ -> cat = "Plantin"
                            | Insumo _ -> cat = "Insumo"
                            | Otro _ -> cat = "Otro"
                    let matchEstado =
                        match estadoFiltro with
                        | None | Some "" -> true
                        | Some est -> EstadoComercial.aTexto p.EstadoComercial = est
                    matchCat && matchEstado)
            return filtrados |> List.map aDto
        }

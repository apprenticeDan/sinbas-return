namespace Sinbas.Infrastructure

open System
open Dapper
open Dapper.FSharp.PostgreSQL
open Sinbas.Domain
open Sinbas.Application

[<CLIMutable>]
type ProductoRow =
    { id                  : Guid
      categoria           : string
      genero              : string option
      epiteto             : string option
      observaciones_nc    : string option
      nombres_comunes     : string option
      etapa_desarrollo    : string option
      nombre_insumo       : string option
      marca_insumo        : string option
      descripcion_insumo  : string option
      empaque             : string option
      unidad_manejo       : string
      gramos_nominales    : decimal option
      trazabilidad        : string
      precio_oficial      : decimal option
      precio_moneda       : string option
      precio_fecha        : DateTime option
      precio_usuario_id   : Guid option
      estado_comercial    : string
      activo              : bool
      observaciones       : string option }

module private CatalogTables =
    let productoTable = table'<ProductoRow> "producto"

module CatalogRepository =

    let private mapUnidad (nombre: string) : UnidadMedida =
        match (if isNull nombre then "" else nombre.Trim().ToLowerInvariant()) with
        | "gramo" | "g" -> Gramo
        | "kilogramo" | "kg" -> Kilogramo
        | "mililitro" | "ml" -> Mililitro
        | "litro" | "l" -> Litro
        | _ -> UnidadDiscreta

    let private mapUnidadTexto (u: UnidadMedida) : string =
        UnidadMedida.aTexto u

    let private reconstruirProducto (row: ProductoRow) : Result<Producto, ErrorIntegridad> =
        let prodId = ProductoId row.id
        let unidad = mapUnidad row.unidad_manejo
        let empaque = defaultArg row.empaque "Unidad"
        match Presentacion.reconstruir empaque (defaultArg row.gramos_nominales 1m) unidad with
        | Error err ->
            Error { Entidad = "producto"
                    RegistroId = string row.id
                    Campo = "gramos_nominales"
                    ValorCrudo = row.gramos_nominales |> Option.map string
                    ErrorDominio = err }
        | Ok presentacion ->
        let trazabilidad = if row.trazabilidad = "PorLote" then PorLote else Simple
        
        let nombresComunesList =
            row.nombres_comunes
            |> Option.map (fun s ->
                s.Split(',')
                |> Array.choose (fun n ->
                    match NombreComun.crear n with Ok nc -> Some nc | Error _ -> None)
                |> Array.toList)
            |> Option.defaultValue []

        let ncOpt =
            match row.genero, row.epiteto with
            | Some g, Some e ->
                match NombreCientifico.crear g e row.observaciones_nc with
                | Ok nc -> Some nc
                | Error _ -> None
            | _ -> None

        let ncFallback = NombreCientifico.reconstruir "Indefinido" "sp." None
        let categoria =
            match row.categoria with
            | "Semilla" ->
                let nc = defaultArg ncOpt ncFallback
                Semilla(nc, nombresComunesList)
            | "Plantin" ->
                let nc = defaultArg ncOpt ncFallback
                Plantin(nc, nombresComunesList, row.etapa_desarrollo)
            | "Insumo" ->
                Insumo(defaultArg row.nombre_insumo "Insumo", row.marca_insumo, row.descripcion_insumo)
            | _ ->
                // NOTA REFACTOR: Se eliminó la variante 'Otro'.
                // Registros de productos antiguos o desconocidos se asignan con seguridad
                // a la categoría 'Insumo' con su marca y descripción correspondientes.
                Insumo(defaultArg row.nombre_insumo "Insumo General", None, row.observaciones)

        let precioOpt =
            row.precio_oficial
            |> Option.map (fun valor ->
                { Valor = valor
                  Moneda = defaultArg row.precio_moneda "BOB"
                  ModificadoPor = row.precio_usuario_id |> Option.map UsuarioId
                  FechaActualizacion = row.precio_fecha })

        match EstadoComercial.desdeTexto row.estado_comercial with
        | Error err ->
            Error { Entidad = "producto"
                    RegistroId = string row.id
                    Campo = "estado_comercial"
                    ValorCrudo = Some row.estado_comercial
                    ErrorDominio = err }
        | Ok estadoComercial ->

        Ok { Base =
                { Id = prodId
                  Presentacion = presentacion
                  Trazabilidad = trazabilidad
                  Activo = row.activo
                  Observaciones = row.observaciones }
             Categoria = categoria
             PrecioOficial = precioOpt
             EstadoComercial = estadoComercial }

    let guardar (producto: Producto) : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            let (ProductoId pId) = producto.Base.Id
            let unidadTexto = mapUnidadTexto producto.Base.Presentacion.Unidad
            let gramos = Some producto.Base.Presentacion.ContenidoNominal

            let (catTexto, genero, epiteto, obsNc, nomComunes, etapa, nomInsumo, marcaInsumo, descInsumo) =
                match producto.Categoria with
                | Semilla(nc, ncs) ->
                    let coms = ncs |> List.map NombreComun.valor |> String.concat ", "
                    ("Semilla", Some nc.Genero, Some nc.Epiteto, nc.Observaciones, Some coms, None, None, None, None)
                | Plantin(nc, ncs, etapa) ->
                    let coms = ncs |> List.map NombreComun.valor |> String.concat ", "
                    ("Plantin", Some nc.Genero, Some nc.Epiteto, nc.Observaciones, Some coms, etapa, None, None, None)
                | Insumo(n, m, d) ->
                    ("Insumo", None, None, None, None, None, Some n, m, d)

            let (precioValor, moneda, precioFecha, usuarioId) =
                match producto.PrecioOficial with
                | Some p -> (Some p.Valor, Some p.Moneda, p.FechaActualizacion, p.ModificadoPor |> Option.map (fun (UsuarioId uId) -> uId))
                | None -> (None, None, None, None)

            let row =
                { id = pId
                  categoria = catTexto
                  genero = genero
                  epiteto = epiteto
                  observaciones_nc = obsNc
                  nombres_comunes = nomComunes
                  etapa_desarrollo = etapa
                  nombre_insumo = nomInsumo
                  marca_insumo = marcaInsumo
                  descripcion_insumo = descInsumo
                  empaque = Some producto.Base.Presentacion.Empaque
                  unidad_manejo = unidadTexto
                  gramos_nominales = gramos
                  trazabilidad = (if producto.Base.Trazabilidad = PorLote then "PorLote" else "Simple")
                  precio_oficial = precioValor
                  precio_moneda = moneda
                  precio_fecha = precioFecha
                  precio_usuario_id = usuarioId
                  estado_comercial = EstadoComercial.aTexto producto.EstadoComercial
                  activo = producto.Base.Activo
                  observaciones = producto.Base.Observaciones }

            try
                let! existente =
                    select {
                        for p in CatalogTables.productoTable do
                        where (p.id = pId)
                    }
                    |> conn.SelectAsync<ProductoRow>
                    |> Async.AwaitTask

                if Seq.isEmpty existente then
                    do! insert {
                            into CatalogTables.productoTable
                            value row
                        }
                        |> conn.InsertAsync
                        |> Async.AwaitTask
                        |> Async.Ignore
                else
                    do! update {
                            for p in CatalogTables.productoTable do
                            set row
                            where (p.id = pId)
                        }
                        |> conn.UpdateAsync
                        |> Async.AwaitTask
                        |> Async.Ignore

                return Ok ()
            with ex ->
                return Error ex.Message
        }

    let buscarPorId (id: ProductoId) : Async<Producto option> =
        async {
            use conn = DbConnection.crear ()
            let (ProductoId rawId) = id

            let! rows =
                select {
                    for p in CatalogTables.productoTable do
                    where (p.id = rawId)
                }
                |> conn.SelectAsync<ProductoRow>
                |> Async.AwaitTask

            return
                Seq.tryHead rows
                |> Option.bind (fun row ->
                    match reconstruirProducto row with
                    | Ok p -> Some p
                    | Error err ->
                        eprintfn "[INTEGRIDAD_CRITICA] CatalogRepository.buscarPorId id=%s campo=%s: %A"
                            err.RegistroId err.Campo err.ErrorDominio
                        None)
        }

    let listarColeccion () : Async<LecturaColeccion<Producto>> =
        async {
            use conn = DbConnection.crear ()

            let! rows =
                select {
                    for p in CatalogTables.productoTable do
                    orderBy p.genero
                }
                |> conn.SelectAsync<ProductoRow>
                |> Async.AwaitTask

            return LecturaColeccion.particionar reconstruirProducto rows
        }

    let listarTodos () : Async<Producto list> =
        async {
            let! coleccion = listarColeccion ()
            return coleccion.Validos
        }

module LaboratorioTests

open System
open Xunit
open Sinbas.Domain
open Sinbas.Application


// ─────────────────────────────────────────────────────────────
// Trazabilidad: RF06 | RF07 | RN01 | RN12 | CU-05 | CU-06 | F-LAB-02 | F-LAB-03 | F-LAB-05
// ─────────────────────────────────────────────────────────────

let prodGuid = Guid.Parse("01917f3a-0003-7000-8000-000000000001")
let prodId = ProductoId prodGuid

let sampleLoteGuid = Guid.Parse("01917f3a-0004-7000-8000-000000000001")
let loteId = LoteId sampleLoteGuid

let crearLoteBase () =
    let codigo =
        match CodigoLote.desdeString "SWIETMAC-02608-01" with
        | Ok c -> c
        | Error e -> failwithf "Codigo invalido: %A" e

    let cantInicial = { Valor = 50m; Unidad = Kilogramo }
    match Lote.crear loteId codigo prodId (Some "Bosque Chiquitano") cantInicial (DateOnly(2026, 8, 15)) (Some "Almacén Central") None with
    | Ok l -> l
    | Error e -> failwithf "Error creando lote: %A" e

// ─────────────────────────────────────────────────────────────
// Tests de Value Object PorcentajeCalidad
// ─────────────────────────────────────────────────────────────

[<Theory>]
[<InlineData(0.0)>]
[<InlineData(50.5)>]
[<InlineData(88.0)>]
[<InlineData(100.0)>]
let ``PorcentajeCalidad acepta valores entre 0 y 100`` (valor: double) =
    let v = decimal valor
    match PorcentajeCalidad.crear v with
    | Ok p -> Assert.Equal(v, PorcentajeCalidad.valor p)
    | Error err -> failwithf "Deberia aceptar %M pero fallo: %A" v err

[<Theory>]
[<InlineData(-0.01)>]
[<InlineData(-10.0)>]
[<InlineData(100.01)>]
[<InlineData(150.0)>]
let ``PorcentajeCalidad rechaza valores menores a 0 o mayores a 100`` (valor: double) =
    let v = decimal valor
    match PorcentajeCalidad.crear v with
    | Error (PorcentajeInvalido msg) -> Assert.Contains("entre 0%", msg)
    | Ok p -> failwithf "Deberia rechazar %M pero retorno Ok: %A" v p
    | Error err -> failwithf "Se esperaba PorcentajeInvalido pero obtuvo: %A" err

// ─────────────────────────────────────────────────────────────
// Tests de F-LAB-05: evaluarDictamenCalidad
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``evaluarDictamenCalidad retorna Aprobado cuando todos los parametros cumplen estandares forestales`` () =
    let germ = match PorcentajeCalidad.crear 85.0m with Ok p -> p | Error _ -> failwith ""
    let pureza = match PorcentajeCalidad.crear 95.0m with Ok p -> p | Error _ -> failwith ""
    let hum = match PorcentajeCalidad.crear 9.0m with Ok p -> p | Error _ -> failwith ""
    let viab = match PorcentajeCalidad.crear 90.0m with Ok p -> p | Error _ -> failwith ""

    let dictamen = AnalisisLaboratorio.evaluarDictamenCalidad germ pureza hum viab
    Assert.Equal(DictamenCalidad.Aprobado, dictamen)

[<Fact>]
let ``evaluarDictamenCalidad retorna Rechazado cuando la germinacion esta por debajo del umbral`` () =
    let germBaja = match PorcentajeCalidad.crear 45.0m with Ok p -> p | Error _ -> failwith ""
    let pureza = match PorcentajeCalidad.crear 95.0m with Ok p -> p | Error _ -> failwith ""
    let hum = match PorcentajeCalidad.crear 9.0m with Ok p -> p | Error _ -> failwith ""
    let viab = match PorcentajeCalidad.crear 50.0m with Ok p -> p | Error _ -> failwith ""

    let dictamen = AnalisisLaboratorio.evaluarDictamenCalidad germBaja pureza hum viab
    Assert.Equal(DictamenCalidad.Rechazado, dictamen)

[<Fact>]
let ``evaluarDictamenCalidad retorna Rechazado cuando la humedad supera el limite maximo permitido`` () =
    let germ = match PorcentajeCalidad.crear 85.0m with Ok p -> p | Error _ -> failwith ""
    let pureza = match PorcentajeCalidad.crear 95.0m with Ok p -> p | Error _ -> failwith ""
    let humAlta = match PorcentajeCalidad.crear 18.0m with Ok p -> p | Error _ -> failwith ""
    let viab = match PorcentajeCalidad.crear 90.0m with Ok p -> p | Error _ -> failwith ""

    let dictamen = AnalisisLaboratorio.evaluarDictamenCalidad germ pureza humAlta viab
    Assert.Equal(DictamenCalidad.Rechazado, dictamen)

// ─────────────────────────────────────────────────────────────
// Tests de Invariantes de AnalisisLaboratorio (rechazo de negativos)
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``AnalisisLaboratorio.crear rechaza semillasPurasKg o semillasImpurezasKg negativas`` () =
    let labId = LaboratorioId (Guid.Parse("01917f3a-0005-7000-8000-000000000001"))
    let fecha = DateOnly(2026, 8, 20)
    let germ = match PorcentajeCalidad.crear 80.0m with Ok p -> p | Error _ -> failwith ""
    let pureza = match PorcentajeCalidad.crear 90.0m with Ok p -> p | Error _ -> failwith ""
    let hum = match PorcentajeCalidad.crear 10.0m with Ok p -> p | Error _ -> failwith ""
    let viab = match PorcentajeCalidad.crear 85.0m with Ok p -> p | Error _ -> failwith ""

    // Caso semillas puras negativa
    match AnalisisLaboratorio.crear labId loteId fecha germ pureza hum viab -1 100 DictamenCalidad.Aprobado None with
    | Error (CantidadInvalida msg) -> Assert.Contains("semillas puras", msg)
    | res -> failwithf "Debio rechazar semillas puras negativas, obtuvo: %A" res

    // Caso impurezas negativa
    match AnalisisLaboratorio.crear labId loteId fecha germ pureza hum viab 15000 -50 DictamenCalidad.Aprobado None with
    | Error (CantidadInvalida msg) -> Assert.Contains("impurezas", msg)
    | res -> failwithf "Debio rechazar impurezas negativas, obtuvo: %A" res

// ─────────────────────────────────────────────────────────────
// Tests de RN12: Exclusión de stock para lotes rechazados por laboratorio
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``Lote con analisis rechazado queda marcado como Rechazado y excluido de stock disponible (RN12)`` () =
    let lote = crearLoteBase ()
    Assert.Equal(Activo, lote.Estado)

    // Al aplicar dictamen Rechazado
    let loteRechazado = Lote.aplicarDictamenLaboratorio DictamenCalidad.Rechazado lote
    Assert.Equal(Rechazado, loteRechazado.Estado)
    Assert.False(Lote.estaActivo loteRechazado)

    // Stock.stockProducto no lo suma
    let stock = Stock.stockProducto prodId [loteRechazado]
    Assert.Equal(0m, stock)

    // Fifo.resolverFIFO no lo asigna
    let req = { Valor = 1000m; Unidad = Gramo }
    match Fifo.resolverFIFO prodId req [loteRechazado] with
    | Error (StockInsuficiente _) -> ()
    | res -> failwithf "FIFO no debio asignar lote rechazado, obtuvo: %A" res

// ─────────────────────────────────────────────────────────────
// Tests de RN01 | RF07 | F-LAB-03: Etiquetado condicionado a análisis
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``Construir datos etiqueta sin analisis de laboratorio retorna error SinAnalisisLaboratorio (RN01)`` () =
    let lote = crearLoteBase ()
    match Etiqueta.construirDatosEtiqueta lote "Mara (Swietenia macrophylla)" None with
    | Error (SinAnalisisLaboratorio msg) ->
        Assert.Contains("no cuenta con análisis de laboratorio", msg)
    | res ->
        failwithf "Debio retornar SinAnalisisLaboratorio pero obtuvo: %A" res

[<Fact>]
let ``Construir datos etiqueta con analisis aprobado retorna estructura completa de DatosEtiqueta (RF07)`` () =
    let lote = crearLoteBase ()
    let labId = LaboratorioId (Guid.Parse("01917f3a-0005-7000-8000-000000000001"))
    let fecha = DateOnly(2026, 8, 20)
    let germ = match PorcentajeCalidad.crear 88.5m with Ok p -> p | Error _ -> failwith ""
    let pureza = match PorcentajeCalidad.crear 95.0m with Ok p -> p | Error _ -> failwith ""
    let hum = match PorcentajeCalidad.crear 8.0m with Ok p -> p | Error _ -> failwith ""
    let viab = match PorcentajeCalidad.crear 90.0m with Ok p -> p | Error _ -> failwith ""

    let analisis =
        match AnalisisLaboratorio.crear labId loteId fecha germ pureza hum viab 18000 500 DictamenCalidad.Aprobado (Some "Excelente calidad") with
        | Ok a -> a
        | Error e -> failwithf "Error creando analisis: %A" e

    match Etiqueta.construirDatosEtiqueta lote "Mara (Swietenia macrophylla)" (Some analisis) with
    | Ok etiqueta ->
        Assert.Equal(lote.Id, etiqueta.LoteId)
        Assert.Equal("SWIETMAC-02608-01", etiqueta.CodigoLote)
        Assert.Equal("Mara (Swietenia macrophylla)", etiqueta.NombreProducto)
        Assert.Equal(88.5m, etiqueta.GerminacionPorcentaje)
        Assert.Equal(95.0m, etiqueta.PurezaPorcentaje)
        Assert.Equal(8.0m, etiqueta.HumedadPorcentaje)
        Assert.Equal(90.0m, etiqueta.ViabilidadPorcentaje)
        Assert.Equal(18000, etiqueta.SemillasPurasKg)
        Assert.Equal("Aprobado", etiqueta.Dictamen)
        Assert.Equal(Some "Excelente calidad", etiqueta.Observaciones)
    | Error err ->
        failwithf "Fallo inesperado al construir etiqueta con analisis: %A" err

// ─────────────────────────────────────────────────────────────
// Tests de Ficha Técnica de Lote (RF14 / CU-14 / F-LAB-06)
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``// T8 & T9: consultarFichaTecnicaLote consolida lote, producto y ultimo dictamen de analisis`` () =
    async {
        let lote = crearLoteBase ()
        let (LoteId lId) = lote.Id

        let nc = match NombreCientifico.crear "Swietenia" "macrophylla" None with Ok n -> n | Error e -> failwithf "%A" e
        let prod = Producto.crearBorradorConUnidad lote.ProductoId Kilogramo PorLote (Semilla(nc, [])) None

        let labId = LaboratorioId (Guid.NewGuid())
        let germ = match PorcentajeCalidad.crear 91.0m with Ok p -> p | Error _ -> failwith ""
        let pureza = match PorcentajeCalidad.crear 97.0m with Ok p -> p | Error _ -> failwith ""
        let hum = match PorcentajeCalidad.crear 7.5m with Ok p -> p | Error _ -> failwith ""
        let viab = match PorcentajeCalidad.crear 92.0m with Ok p -> p | Error _ -> failwith ""
        let analisis =
            match AnalisisLaboratorio.crear labId lote.Id (DateOnly(2026, 8, 22)) germ pureza hum viab 19000 300 DictamenCalidad.Aprobado (Some "Excelente lote") with
            | Ok a -> a
            | Error e -> failwithf "%A" e

        let fakeObtenerLote (id: LoteId) = async { if id = lote.Id then return Some lote else return None }
        let fakeObtenerProducto (id: ProductoId) = async { if id = prod.Base.Id then return Some prod else return None }
        let fakeListarAnalisis (id: LoteId) = async { return [ analisis ] }

        let! res = LoteService.consultarFichaTecnicaLote fakeObtenerLote fakeObtenerProducto fakeListarAnalisis (lId.ToString())
        match res with
        | Ok dto ->
            Assert.Equal(lId.ToString(), dto.Id)
            Assert.Equal("SWIETMAC-02608-01", dto.Codigo)
            Assert.Equal("Swietenia macrophylla", dto.NombreProducto)
            Assert.Equal("Semilla", dto.Categoria)
            Assert.Equal(Some "Swietenia", dto.Genero)
            Assert.Equal(Some "macrophylla", dto.Epiteto)
            Assert.Equal(Some "Aprobado", dto.UltimoDictamen)
            Assert.Single(dto.HistorialAnalisis) |> ignore
            Assert.Equal(91.0m, dto.HistorialAnalisis.Head.Germinacion)
        | Error err -> failwithf "Fallo consultarFichaTecnicaLote: %s" err
    } |> Async.RunSynchronously

[<Fact>]
let ``// T10: consultarFichaTecnicaLote para lote recien ingresado sin analisis retorna historial vacio y ultimoDictamen None`` () =
    async {
        let lote = crearLoteBase ()
        let (LoteId lId) = lote.Id
        let nc = match NombreCientifico.crear "Cedrela" "odorata" None with Ok n -> n | Error e -> failwithf "%A" e
        let prod = Producto.crearBorradorConUnidad lote.ProductoId Kilogramo PorLote (Semilla(nc, [])) None

        let fakeObtenerLote (id: LoteId) = async { return Some lote }
        let fakeObtenerProducto (id: ProductoId) = async { return Some prod }
        let fakeListarAnalisis (id: LoteId) = async { return [] }

        let! res = LoteService.consultarFichaTecnicaLote fakeObtenerLote fakeObtenerProducto fakeListarAnalisis (lId.ToString())
        match res with
        | Ok dto ->
            Assert.Empty(dto.HistorialAnalisis)
            Assert.True(dto.UltimoDictamen.IsNone)
        | Error err -> failwithf "Fallo inesperado: %s" err
    } |> Async.RunSynchronously

[<Fact>]
let ``// T11: consultarFichaTecnicaLote con ID inexistente retorna error 404/NotFound`` () =
    async {
        let fakeObtenerLote (_: LoteId) = async { return None }
        let fakeObtenerProducto (_: ProductoId) = async { return None }
        let fakeListarAnalisis (_: LoteId) = async { return [] }

        let! res = LoteService.consultarFichaTecnicaLote fakeObtenerLote fakeObtenerProducto fakeListarAnalisis (Guid.NewGuid().ToString())
        match res with
        | Error msg -> Assert.Contains("no se encontró", msg, StringComparison.OrdinalIgnoreCase)
        | Ok _ -> failwith "Debería retornar error para lote no encontrado"
    } |> Async.RunSynchronously


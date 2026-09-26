namespace Sinbas.Domain

open System

// ─────────────────────────────────────────────────────────────
// Nombre científico
// ─────────────────────────────────────────────────────────────

type NombreCientifico =
    private
        { _Genero: string
          _Epiteto: string
          _Observaciones: string option }
    member this.Genero = this._Genero
    member this.Epiteto = this._Epiteto
    member this.Observaciones = this._Observaciones

module NombreCientifico =
    // ── Accessors ────────────────────────────────────────────────────────────
    let genero (n: NombreCientifico) : string = n.Genero
    let epiteto (n: NombreCientifico) : string = n.Epiteto
    let observaciones (n: NombreCientifico) : string option = n.Observaciones

    // ── Constructores ────────────────────────────────────────────────────────
    let crear (genero: string) (epiteto: string) (obs: string option) : Result<NombreCientifico, DomainError> =
        if String.IsNullOrWhiteSpace genero then
            Error(NombreInvalido "El género no puede estar vacío")
        elif String.IsNullOrWhiteSpace epiteto then
            Error(NombreInvalido "El epíteto no puede estar vacío")
        else
            let obsLimpia =
                obs
                |> Option.bind (fun s ->
                    let t = s.Trim()
                    if String.IsNullOrWhiteSpace t then None else Some t)

            Ok
                { _Genero = genero.Trim()
                  _Epiteto = epiteto.Trim()
                  _Observaciones = obsLimpia }

    /// Reconstruye una instancia de NombreCientifico para persistencia/infraestructura
    let reconstruir (genero: string) (epiteto: string) (obs: string option) : NombreCientifico =
        let g = if String.IsNullOrWhiteSpace genero then "Indefinido" else genero.Trim()
        let e = if String.IsNullOrWhiteSpace epiteto then "sp." else epiteto.Trim()
        let obsLimpia =
            obs
            |> Option.bind (fun s ->
                let t = s.Trim()
                if String.IsNullOrWhiteSpace t then None else Some t)
        { _Genero = g
          _Epiteto = e
          _Observaciones = obsLimpia }

    let formatear (n: NombreCientifico) : string =
        match n.Observaciones with
        | None -> sprintf "%s %s" n.Genero n.Epiteto
        | Some obs -> sprintf "%s %s %s" n.Genero n.Epiteto obs

// ─────────────────────────────────────────────────────────────
// Nombre común
// ─────────────────────────────────────────────────────────────

type NombreComun = NombreComun of string

module NombreComun =

    let crear (s: string) : Result<NombreComun, DomainError> =
        if String.IsNullOrWhiteSpace s then
            Error(NombreInvalido "El nombre común no puede estar vacío")
        else
            Ok(NombreComun(s.Trim()))

    let valor (NombreComun n) : string = n

// ─────────────────────────────────────────────────────────────
// Trazabilidad
// ─────────────────────────────────────────────────────────────

type Trazabilidad =
    | PorLote
    | Simple

// ─────────────────────────────────────────────────────────────
// Gobernanza de Precios y Estados Comerciales
// ─────────────────────────────────────────────────────────────

type EstadoComercial =
    | PendientePrecioBorrador
    | ActivoParaVenta
    | Inactivo

module EstadoComercial =
    let aTexto = function
        | PendientePrecioBorrador -> "PendientePrecioBorrador"
        | ActivoParaVenta -> "ActivoParaVenta"
        | Inactivo -> "Inactivo"

    let desdeTexto (s: string) =
        match s.Trim() with
        | "ActivoParaVenta" -> ActivoParaVenta
        | "Inactivo" -> Inactivo
        | _ -> PendientePrecioBorrador

type PrecioOficial =
    { Valor: decimal
      Moneda: string
      ModificadoPor: UsuarioId option
      FechaActualizacion: DateTime option }

// ─────────────────────────────────────────────────────────────
// Categorías del Catálogo de BASFOR
//
// NOTA DE DOMINIO / REFACTOR (observaciones_resumen.txt):
// Se elimina formalmente la categoría comodín 'Otro' para restringir
// el dominio y asegurar estados legales representables en compilación (DDD).
// En BASFOR todo producto comercializable o almacenable pertenece
// exclusivamente a una de estas tres familias:
//   1. Semilla: Material forestal/botánico identificado por nombre científico y nombres comunes.
//   2. Plantin: Material vivo con etapa de desarrollo (vivero, plantón, etc.).
//   3. Insumo: Bienes e insumos agroforestales (sustratos, fungicidas, bolsas, herramientas)
//              con nombre genérico y marca opcional.
// ─────────────────────────────────────────────────────────────

type CategoriaProducto =
    | Semilla of nombreCientifico: NombreCientifico * nombresComunes: NombreComun list
    | Plantin of nombreCientifico: NombreCientifico * nombresComunes: NombreComun list * etapaDesarrollo: string option
    | Insumo of nombre: string * marca: string option * descripcion: string option

// ─────────────────────────────────────────────────────────────
// Base común del producto
// ─────────────────────────────────────────────────────────────

type ProductoBase =
    { Id: ProductoId
      Presentacion: Presentacion
      Trazabilidad: Trazabilidad
      Activo: bool
      Observaciones: string option }
    member this.UnidadManejo = this.Presentacion.Unidad

// ─────────────────────────────────────────────────────────────
// Producto
// ─────────────────────────────────────────────────────────────

type Producto =
    { Base: ProductoBase
      Categoria: CategoriaProducto
      PrecioOficial: PrecioOficial option
      EstadoComercial: EstadoComercial }

module Producto =

    let crearBorrador id (presentacion: Presentacion) trazabilidad categoria observaciones =
        { Base =
            { Id = id
              Presentacion = presentacion
              Trazabilidad = trazabilidad
              Activo = true
              Observaciones = observaciones }
          Categoria = categoria
          PrecioOficial = None
          EstadoComercial = PendientePrecioBorrador }

    /// Helper de conveniencia para crear borrador especificando únicamente la UnidadMedida
    let crearBorradorConUnidad id (unidad: UnidadMedida) trazabilidad categoria observaciones =
        let pres = Presentacion.reconstruir "Unidad" 1m unidad
        crearBorrador id pres trazabilidad categoria observaciones

    let asignarPrecio (monto: decimal) (moneda: string option) (usuarioId: UsuarioId option) (p: Producto) : Result<Producto, DomainError> =
        if monto <= 0m then
            Error (CantidadInvalida (sprintf "El precio debe ser mayor a cero, recibido: %M" monto))
        else
            let nuevoPrecio =
                { Valor = monto
                  Moneda = defaultArg moneda "BOB"
                  ModificadoPor = usuarioId
                  FechaActualizacion = Some DateTime.UtcNow }
            Ok { p with PrecioOficial = Some nuevoPrecio; EstadoComercial = ActivoParaVenta }

    let esAptoParaVenta (p: Producto) : bool =
        p.Base.Activo && p.EstadoComercial = ActivoParaVenta && Option.isSome p.PrecioOficial

    let desactivar (p: Producto) : Producto =
        { p with Base = { p.Base with Activo = false }; EstadoComercial = Inactivo }

    let nombreVisible (p: Producto) : string =
        match p.Categoria with
        | Semilla(nc, _) -> NombreCientifico.formatear nc
        | Plantin(nc, _, _) -> NombreCientifico.formatear nc
        | Insumo(nombre, marcaOpt, _) ->
            match marcaOpt with
            | Some marca when not (String.IsNullOrWhiteSpace marca) -> sprintf "%s (%s)" nombre (marca.Trim())
            | _ -> nombre

    let nombresComunes (p: Producto) : NombreComun list =
        match p.Categoria with
        | Semilla(_, ncs) -> ncs
        | Plantin(_, ncs, _) -> ncs
        | Insumo _ -> []

    let nombreCientifico (p: Producto) : NombreCientifico option =
        match p.Categoria with
        | Semilla(nc, _) -> Some nc
        | Plantin(nc, _, _) -> Some nc
        | Insumo _ -> None

    let requiereLote (p: Producto) : bool =
        p.Base.Trazabilidad = PorLote

    let presentacion (p: Producto) : Presentacion =
        p.Base.Presentacion

    let unidad (p: Producto) : UnidadMedida =
        p.Base.Presentacion.Unidad

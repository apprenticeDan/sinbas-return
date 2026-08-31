namespace Sinbas.Domain

open System

// ─────────────────────────────────────────────────────────────
// Nombre científico
// ─────────────────────────────────────────────────────────────

type NombreCientifico =
    { Genero: string
      Epiteto: string
      Observaciones: string option }

module NombreCientifico =

    let crear (genero: string) (epiteto: string) (obs: string option) : Result<NombreCientifico, DomainError> =
        if String.IsNullOrWhiteSpace genero then
            Error(NombreInvalido "El género no puede estar vacío")
        elif String.IsNullOrWhiteSpace epiteto then
            Error(NombreInvalido "El epíteto no puede estar vacío")
        else
            Ok
                { Genero = genero.Trim()
                  Epiteto = epiteto.Trim()
                  Observaciones = obs |> Option.map (fun s -> s.Trim()) }

    let formatear n =
        match n.Observaciones with
        | None -> sprintf "%s %s" n.Genero n.Epiteto
        | Some obs -> sprintf "%s %s %s" n.Genero n.Epiteto obs

// ─────────────────────────────────────────────────────────────
// Nombre común
// ─────────────────────────────────────────────────────────────

type NombreComun = NombreComun of string

module NombreComun =

    let crear s =
        if String.IsNullOrWhiteSpace s then
            Error(NombreInvalido "El nombre común no puede estar vacío")
        else
            Ok(NombreComun(s.Trim()))

    let valor (NombreComun n) = n

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
// Base común
// ─────────────────────────────────────────────────────────────

type ProductoBase =
    { Id: ProductoId
      UnidadManejo: Unidad
      Trazabilidad: Trazabilidad
      Activo: bool
      EsRestringidoParaMayores: bool
      Observaciones: string option }

// ─────────────────────────────────────────────────────────────
// Categorías
// ─────────────────────────────────────────────────────────────

type CategoriaProducto =
    | Semilla of nombreCientifico: NombreCientifico * nombresComunes: NombreComun list

    | Plantin of nombreCientifico: NombreCientifico * nombresComunes: NombreComun list * etapaDesarrollo: string option

    | Insumo of nombre: string * marca: string option * descripcion: string option

    | Otro of nombre: string * descripcion: string option

// ─────────────────────────────────────────────────────────────
// Producto
// ─────────────────────────────────────────────────────────────

type Producto =
    { Base: ProductoBase
      Categoria: CategoriaProducto
      PrecioOficial: PrecioOficial option
      EstadoComercial: EstadoComercial }

module Producto =

    let crearBorrador id unidad trazabilidad categoria esRestringido observaciones =
        { Base =
            { Id = id
              UnidadManejo = unidad
              Trazabilidad = trazabilidad
              Activo = true
              EsRestringidoParaMayores = esRestringido
              Observaciones = observaciones }
          Categoria = categoria
          PrecioOficial = None
          EstadoComercial = PendientePrecioBorrador }

    let requiereMayorEdad (p: Producto) = p.Base.EsRestringidoParaMayores


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

    let esAptoParaVenta p =
        p.Base.Activo && p.EstadoComercial = ActivoParaVenta && Option.isSome p.PrecioOficial

    let desactivar p =
        { p with Base = { p.Base with Activo = false }; EstadoComercial = Inactivo }

    let nombreVisible p =
        match p.Categoria with
        | Semilla(nc, _) -> NombreCientifico.formatear nc

        | Plantin(nc, _, _) -> NombreCientifico.formatear nc

        | Insumo(nombre, _, _) -> nombre

        | Otro(nombre, _) -> nombre

    let nombresComunes p =
        match p.Categoria with
        | Semilla(_, ncs) -> ncs
        | Plantin(_, ncs, _) -> ncs
        | _ -> []

    let nombreCientifico (p: Producto) =
        match p.Categoria with
        | Semilla(nc, _) -> Some nc
        | Plantin(nc, _, _) -> Some nc
        | _ -> None

    let requiereLote p = p.Base.Trazabilidad = PorLote

    let unidad p = p.Base.UnidadManejo


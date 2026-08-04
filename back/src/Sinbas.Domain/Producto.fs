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
// Base común
// ─────────────────────────────────────────────────────────────

type ProductoBase =
    { Id: ProductoId
      UnidadManejo: Unidad
      Trazabilidad: Trazabilidad
      Activo: bool
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
      Categoria: CategoriaProducto }

module Producto =

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

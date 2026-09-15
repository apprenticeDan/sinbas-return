namespace Sinbas.Domain

open System

type EstadoEmpleado =
    | Activo
    | Inactivo

type Empleado =
    { Id: EmpleadoId
      DatosPersonales: Persona // Agregación / Composición en lugar de herencia
      Estado: EstadoEmpleado }
    member this.NombreCompleto = this.DatosPersonales.NombreCompleto
    member this.Nombres = this.DatosPersonales.Nombres
    member this.ApellidoPaterno = this.DatosPersonales.ApellidoPaterno
    member this.ApellidoMaterno = this.DatosPersonales.ApellidoMaterno
    member this.CI = this.DatosPersonales.CI
    member this.Telefono = this.DatosPersonales.Telefono
    member this.Email = this.DatosPersonales.Email

module Empleado =

    let crear (persona: Persona) : Empleado =
        { Id = EmpleadoId (Identidad.nuevo ())
          DatosPersonales = persona
          Estado = Activo }

    let crearDeDatos
        (nombresRaw: string)
        (paternoRaw: string option)
        (maternoRaw: string option)
        (ci: CI)
        (telefonoRaw: string option)
        (emailRaw: string option)
        : Result<Empleado, DomainError> =
        Persona.crear nombresRaw paternoRaw maternoRaw ci telefonoRaw emailRaw
        |> Result.map crear

    let actualizar
        (nombresRaw: string)
        (paternoRaw: string option)
        (maternoRaw: string option)
        (ci: CI)
        (telefonoRaw: string option)
        (emailRaw: string option)
        (e: Empleado)
        : Result<Empleado, DomainError> =
        match Persona.crear nombresRaw paternoRaw maternoRaw ci telefonoRaw emailRaw with
        | Error err -> Error err
        | Ok p ->
            let personaActualizada = { p with Id = e.DatosPersonales.Id }
            Ok { e with DatosPersonales = personaActualizada }

    let actualizarDatosPersonales (nuevaPersona: Persona) (e: Empleado) =
        { e with DatosPersonales = nuevaPersona }

    let reconstruir id (persona: Persona) estado =
        { Id = EmpleadoId id
          DatosPersonales = persona
          Estado = estado }

    let inactivar (e: Empleado) =
        { e with Estado = Inactivo }

    let activar (e: Empleado) =
        { e with Estado = Activo }

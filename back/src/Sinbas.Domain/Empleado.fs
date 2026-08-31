namespace Sinbas.Domain

type EstadoEmpleado =
    | Activo
    | Inactivo

type Empleado =
    { Id: EmpleadoId
      NombreCompleto: string
      CI: CI
      Telefono: string option
      Email: string option
      Estado: EstadoEmpleado }

module Empleado =

    let crear (nombreCompleto: string) (ci: CI) (telefono: string option) (email: string option) : Result<Empleado, DomainError> =
        match Validacion.validarTextoNombre "Nombre Completo" nombreCompleto with
        | Error err -> Error err
        | Ok nombreLimpio ->
            match telefono with
            | Some t ->
                match Validacion.validarTelefono "Teléfono" t with
                | Error err -> Error err
                | Ok telLimpio ->
                    Ok { Id = EmpleadoId (Identidad.nuevo ())
                         NombreCompleto = nombreLimpio
                         CI = ci
                         Telefono = Some telLimpio
                         Email = email |> Option.map (fun e -> e.Trim().ToLowerInvariant())
                         Estado = Activo }
            | None ->
                Ok { Id = EmpleadoId (Identidad.nuevo ())
                     NombreCompleto = nombreLimpio
                     CI = ci
                     Telefono = None
                     Email = email |> Option.map (fun e -> e.Trim().ToLowerInvariant())
                     Estado = Activo }

    let reconstruir id nombreCompleto ci telefono email estado =
        { Id = EmpleadoId id
          NombreCompleto = nombreCompleto
          CI = ci
          Telefono = telefono
          Email = email
          Estado = estado }

    let inactivar (e: Empleado) =
        { e with Estado = Inactivo }

    let activar (e: Empleado) =
        { e with Estado = Activo }

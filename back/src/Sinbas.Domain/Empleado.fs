namespace Sinbas.Domain

open System

type EstadoEmpleado =
    | Activo
    | Inactivo

module private EmpleadoHelpers =
    let formatearNombreCompleto (nombres: string) (paterno: string option) (materno: string option) : string =
        let p = paterno |> Option.bind (fun s -> let t = s.Trim() in if String.IsNullOrWhiteSpace t then None else Some t)
        let m = materno |> Option.bind (fun s -> let t = s.Trim() in if String.IsNullOrWhiteSpace t then None else Some t)
        match p, m with
        | Some paternoVal, Some maternoVal -> sprintf "%s %s, %s" paternoVal maternoVal nombres
        | Some paternoVal, None -> sprintf "%s, %s" paternoVal nombres
        | None, Some maternoVal -> sprintf "%s, %s" maternoVal nombres
        | None, None -> nombres

type Empleado =
    { Id: EmpleadoId
      Nombres: string
      ApellidoPaterno: string option
      ApellidoMaterno: string option
      CI: CI
      Telefono: string option
      Email: string option
      Estado: EstadoEmpleado }
    member this.NombreCompleto =
        EmpleadoHelpers.formatearNombreCompleto this.Nombres this.ApellidoPaterno this.ApellidoMaterno

module Empleado =

    let formatearNombreCompleto = EmpleadoHelpers.formatearNombreCompleto

    let crear
        (nombresRaw: string)
        (paternoRaw: string option)
        (maternoRaw: string option)
        (ci: CI)
        (telefonoRaw: string option)
        (emailRaw: string option)
        : Result<Empleado, DomainError> =

        match Validacion.validarTextoNombre "Nombres" nombresRaw with
        | Error err -> Error err
        | Ok nombresLimpio ->
            let paternoValidado =
                match paternoRaw with
                | Some p when not (String.IsNullOrWhiteSpace p) ->
                    Validacion.validarTextoNombre "Apellido Paterno" p |> Result.map Some
                | _ -> Ok None

            match paternoValidado with
            | Error err -> Error err
            | Ok pLimpio ->
                let maternoValidado =
                    match maternoRaw with
                    | Some m when not (String.IsNullOrWhiteSpace m) ->
                        Validacion.validarTextoNombre "Apellido Materno" m |> Result.map Some
                    | _ -> Ok None

                match maternoValidado with
                | Error err -> Error err
                | Ok mLimpio ->
                    if Option.isNone pLimpio && Option.isNone mLimpio then
                        Error (ValorRequerido "Debe registrar al menos un apellido (paterno o materno)")
                    else
                        let telValidado =
                            match telefonoRaw with
                            | Some t when not (String.IsNullOrWhiteSpace t) ->
                                Validacion.validarTelefono "Teléfono" t |> Result.map Some
                            | _ -> Ok None

                        match telValidado with
                        | Error err -> Error err
                        | Ok tLimpio ->
                            let emailLimpio =
                                emailRaw
                                |> Option.bind (fun e ->
                                    let trimmed = e.Trim().ToLowerInvariant()
                                    if String.IsNullOrWhiteSpace trimmed then None else Some trimmed)

                            Ok { Id = EmpleadoId (Identidad.nuevo ())
                                 Nombres = nombresLimpio
                                 ApellidoPaterno = pLimpio
                                 ApellidoMaterno = mLimpio
                                 CI = ci
                                 Telefono = tLimpio
                                 Email = emailLimpio
                                 Estado = Activo }

    let actualizar
        (nombresRaw: string)
        (paternoRaw: string option)
        (maternoRaw: string option)
        (ci: CI)
        (telefonoRaw: string option)
        (emailRaw: string option)
        (e: Empleado)
        : Result<Empleado, DomainError> =

        match crear nombresRaw paternoRaw maternoRaw ci telefonoRaw emailRaw with
        | Error err -> Error err
        | Ok nuevo ->
            Ok { nuevo with Id = e.Id; Estado = e.Estado }

    let reconstruir id nombres paterno materno ci telefono email estado =
        { Id = EmpleadoId id
          Nombres = nombres
          ApellidoPaterno = paterno
          ApellidoMaterno = materno
          CI = ci
          Telefono = telefono
          Email = email
          Estado = estado }

    let inactivar (e: Empleado) =
        { e with Estado = Inactivo }

    let activar (e: Empleado) =
        { e with Estado = Activo }


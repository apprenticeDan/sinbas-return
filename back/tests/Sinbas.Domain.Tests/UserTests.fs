module UserTests

open System
open Xunit
open Sinbas.Domain
open Sinbas.Application

let private hashFake (p: string) = PasswordHash ($"hash_{p}")

[<Fact>]
let ``Crear usuario exitosamente con solo apellido materno (caso docente)`` () =
    async {
        let buscarPorNombre _ = async { return Error CredencialesInvalidas }
        let mutable guardado = false
        let guardarUsuarioYEmpleado (u: Usuario) (e: Empleado) =
            async {
                guardado <- true
                Assert.Equal("gtorrico", Usuario.nombreUsuario u |> NombreUsuario.valor)
                Assert.Equal("Gabriel", e.Nombres)
                Assert.Null(e.ApellidoPaterno |> Option.toObj)
                Assert.Equal(Some "Torrico", e.ApellidoMaterno)
                Assert.Equal("Torrico, Gabriel", e.NombreCompleto)
                return Ok ()
            }

        let cmd : CreateUserCommand =
            { EmpleadoId = None
              Nombres = "Gabriel"
              ApellidoPaterno = None
              ApellidoMaterno = Some "Torrico"
              CiNumero = "9876543"
              CiComplemento = Some "CB"
              Telefono = Some "+591 72223344"
              Email = Some "gabriel.torrico@umss.edu.bo"
              NombreUsuario = "gtorrico"
              Contrasena = "contrasenaSegura123"
              Roles = [| "Almacen"; "Laboratorio" |] }

        let! res = UserUseCase.crearUsuario buscarPorNombre guardarUsuarioYEmpleado hashFake cmd
        match res with
        | Ok () -> Assert.True(guardado)
        | Error err -> failwithf "Debería haber creado usuario con solo apellido materno: %A" err
    }

[<Fact>]
let ``Crear usuario falla si no tiene ningun apellido`` () =
    async {
        let buscarPorNombre _ = async { return Error CredencialesInvalidas }
        let guardarUsuarioYEmpleado _ _ = async { return Ok () }

        let cmd : CreateUserCommand =
            { EmpleadoId = None
              Nombres = "Juan Daniel"
              ApellidoPaterno = None
              ApellidoMaterno = None
              CiNumero = "1234567"
              CiComplemento = None
              Telefono = None
              Email = None
              NombreUsuario = "jdaniel"
              Contrasena = "contrasenaSegura123"
              Roles = [| "Almacen" |] }

        let! res = UserUseCase.crearUsuario buscarPorNombre guardarUsuarioYEmpleado hashFake cmd
        match res with
        | Error (NombreUsuarioInvalido msg) ->
            Assert.Contains("al menos un apellido", msg)
        | res -> failwithf "Debería haber fallado por falta de apellido, obtuvo: %A" res
    }

[<Fact>]
let ``Crear usuario falla si el nombre contiene numeros`` () =
    async {
        let buscarPorNombre _ = async { return Error CredencialesInvalidas }
        let guardarUsuarioYEmpleado _ _ = async { return Ok () }

        let cmd : CreateUserCommand =
            { EmpleadoId = None
              Nombres = "Gabriel123"
              ApellidoPaterno = Some "Torrico"
              ApellidoMaterno = None
              CiNumero = "1234567"
              CiComplemento = None
              Telefono = None
              Email = None
              NombreUsuario = "gtorrico"
              Contrasena = "contrasenaSegura123"
              Roles = [| "Almacen" |] }

        let! res = UserUseCase.crearUsuario buscarPorNombre guardarUsuarioYEmpleado hashFake cmd
        match res with
        | Error (NombreUsuarioInvalido msg) ->
            Assert.Contains("números", msg)
        | res -> failwithf "Debería haber fallado por números en el nombre, obtuvo: %A" res
    }

[<Fact>]
let ``Crear usuario falla si el nombre de usuario ya esta registrado`` () =
    async {
        let usuarioExistente =
            let ci = match CI.crear "111" None with Ok c -> c | Error _ -> failwith "CI"
            let emp = match Empleado.crear "Admin" (Some "User") None ci None None with Ok e -> e | Error _ -> failwith "Emp"
            let nombre = match Usuario.validarNombreUsuario "admin" with Ok u -> u | _ -> failwith "nombre"
            let roles = Set.ofList [ match NombreRol.fromString "Administrador" with Ok r -> r | _ -> failwith "rol" ]
            Usuario.crear emp.Id nombre (PasswordHash "h") roles
            |> function Ok u -> u | Error _ -> failwith "U"

        let buscarPorNombre _ = async { return Ok usuarioExistente }
        let guardarUsuarioYEmpleado _ _ = async { return Ok () }

        let cmd : CreateUserCommand =
            { EmpleadoId = None
              Nombres = "Gabriel"
              ApellidoPaterno = Some "Torrico"
              ApellidoMaterno = None
              CiNumero = "9876543"
              CiComplemento = None
              Telefono = None
              Email = None
              NombreUsuario = "admin"
              Contrasena = "contrasenaSegura123"
              Roles = [| "Almacen" |] }

        let! res = UserUseCase.crearUsuario buscarPorNombre guardarUsuarioYEmpleado hashFake cmd
        match res with
        | Error (NombreUsuarioInvalido msg) ->
            Assert.Contains("ya está registrado", msg)
        | res -> failwithf "Debería haber fallado por usuario duplicado, obtuvo: %A" res
    }

[<Fact>]
let ``Actualizar usuario modifica datos de empleado, usuario y roles`` () =
    async {
        let uid = Guid.NewGuid()
        let eid = Guid.NewGuid()
        let ci = match CI.crear "1234567" None with Ok c -> c | Error _ -> failwith "CI"
        let empleadoOriginal =
            Empleado.reconstruir eid "Pedro" (Some "Ramos") None ci None None EstadoEmpleado.Activo
        let usuarioOriginal =
            Usuario.reconstruir uid eid "pedro" "hash_antiguo" (Set.singleton NombreRol.Almacen) EstadoUsuario.Activo

        let buscarUsuarioConEmpleado _ =
            async { return Ok { Usuario = usuarioOriginal; Empleado = empleadoOriginal } }
        let buscarPorNombre _ =
            async { return Error CredencialesInvalidas }

        let mutable actualizado = false
        let guardarUsuarioYEmpleado (u: Usuario) (e: Empleado) =
            async {
                actualizado <- true
                Assert.Equal("pedro.nuevo", Usuario.nombreUsuario u |> NombreUsuario.valor)
                Assert.Equal("Pedro Pablo", e.Nombres)
                Assert.Equal(Some "Ramos", e.ApellidoPaterno)
                Assert.Equal(Some "Suárez", e.ApellidoMaterno)
                Assert.Equal("Ramos Suárez, Pedro Pablo", e.NombreCompleto)
                Assert.True(Usuario.tieneRol NombreRol.Administrador u)
                return Ok ()
            }

        let cmd : UpdateUserCommand =
            { UsuarioId = uid
              Nombres = "Pedro Pablo"
              ApellidoPaterno = Some "Ramos"
              ApellidoMaterno = Some "Suárez"
              CiNumero = "1234567"
              CiComplemento = Some "CB"
              Telefono = Some "+591 77788999"
              Email = Some "pedro@empresa.com"
              NombreUsuario = "pedro.nuevo"
              Roles = [| "Administrador"; "Almacen" |]
              NuevaContrasena = None }

        let! res = UserUseCase.actualizarUsuario buscarUsuarioConEmpleado buscarPorNombre guardarUsuarioYEmpleado hashFake cmd
        match res with
        | Ok () -> Assert.True(actualizado)
        | Error err -> failwithf "Debería haber actualizado correctamente: %A" err
    }

[<Fact>]
let ``Actualizar usuario falla si el nuevo username ya pertenece a otra cuenta`` () =
    async {
        let uid = Guid.NewGuid()
        let eid = Guid.NewGuid()
        let ci = match CI.crear "1234567" None with Ok c -> c | Error _ -> failwith "CI"
        let emp = Empleado.reconstruir eid "Pedro" (Some "Ramos") None ci None None EstadoEmpleado.Activo
        let usr = Usuario.reconstruir uid eid "pedro" "hash_antiguo" (Set.singleton NombreRol.Almacen) EstadoUsuario.Activo

        let otroUsr = Usuario.reconstruir (Guid.NewGuid()) (Guid.NewGuid()) "admin" "hash" (Set.singleton NombreRol.Administrador) EstadoUsuario.Activo

        let buscarUsuarioConEmpleado _ =
            async { return Ok { Usuario = usr; Empleado = emp } }
        let buscarPorNombre _ =
            async { return Ok otroUsr }
        let guardarUsuarioYEmpleado _ _ = async { return Ok () }

        let cmd : UpdateUserCommand =
            { UsuarioId = uid
              Nombres = "Pedro"
              ApellidoPaterno = Some "Ramos"
              ApellidoMaterno = None
              CiNumero = "1234567"
              CiComplemento = None
              Telefono = None
              Email = None
              NombreUsuario = "admin"
              Roles = [| "Almacen" |]
              NuevaContrasena = None }

        let! res = UserUseCase.actualizarUsuario buscarUsuarioConEmpleado buscarPorNombre guardarUsuarioYEmpleado hashFake cmd
        match res with
        | Error (NombreUsuarioInvalido msg) ->
            Assert.Contains("otra cuenta", msg)
        | res -> failwithf "Debería haber fallado por username en uso, obtuvo: %A" res
    }

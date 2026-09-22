module ClienteTests

open System
open Xunit
open Sinbas.Domain
open Sinbas.Application

// ─────────────────────────────────────────────────────────────
// Feature F6: Gestión de Clientes (RF08 | CU-17 | RN18)
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``// RF08 | CU-17 | RN18 - NIT valido con digitos es aceptado`` () =
    match NIT.crear "1028495029" with
    | Ok nit ->
        Assert.Equal("1028495029", NIT.valor nit)
    | Error err ->
        failwithf "Se esperaba Ok, se obtuvo: %A" err

[<Fact>]
let ``// RF08 | CU-17 | RN18 - NIT vacio o en blanco retorna ValorRequerido`` () =
    match NIT.crear "   " with
    | Error (ValorRequerido _) -> ()
    | res -> failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res

[<Fact>]
let ``// RF08 | CU-17 | RN18 - NIT con letras o caracteres no numericos retorna SimbolosNoPermitidos`` () =
    match NIT.crear "10284-A" with
    | Error (SimbolosNoPermitidos _) -> ()
    | res -> failwithf "Se esperaba Error SimbolosNoPermitidos, se obtuvo: %A" res

[<Fact>]
let ``// RF08 | CU-17 | RN18 - RazonSocial valida con puntuacion y ampersand es aceptada`` () =
    match RazonSocial.crear "Agroforestal del Sur & Cía. S.R.L." with
    | Ok rs ->
        Assert.Equal("Agroforestal del Sur & Cía. S.R.L.", RazonSocial.valor rs)
    | Error err ->
        failwithf "Se esperaba Ok, se obtuvo: %A" err

[<Fact>]
let ``// RF08 | CU-17 | RN18 - RazonSocial vacia retorna ValorRequerido`` () =
    match RazonSocial.crear "" with
    | Error (ValorRequerido _) -> ()
    | res -> failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res

[<Fact>]
let ``// RF08 | CU-17 | RN18 - RazonSocial con longitud menor a 2 caracteres retorna NombreInvalido`` () =
    match RazonSocial.crear "A" with
    | Error (NombreInvalido _) -> ()
    | res -> failwithf "Se esperaba Error NombreInvalido, se obtuvo: %A" res

[<Fact>]
let ``// RF08 | CU-17 | RN18 - RazonSocial con caracteres no permitidos retorna SimbolosNoPermitidos`` () =
    match RazonSocial.crear "Empresa <Forestal> *100%*" with
    | Error (SimbolosNoPermitidos _) -> ()
    | res -> failwithf "Se esperaba Error SimbolosNoPermitidos, se obtuvo: %A" res

[<Fact>]
let ``// RF08 | CU-17 | RN18 - Crear Cliente Persona Natural asigna estado Activo y delega nombre`` () =
    let ci = match CI.crear "4892011" None (Some CB) with Ok c -> c | Error e -> failwithf "%A" e
    let persona = match Persona.crear "Carlos" (Some "Mendoza") (Some "Vaca") ci (Some "+591 76543210") (Some "carlos@vivero.bo") with Ok p -> p | Error e -> failwithf "%A" e

    match Cliente.crearNatural persona (Some "+591 76543210") (Some "carlos@vivero.bo") (Some "Av. Blanco Galindo Km 5") with
    | Ok cliente ->
        Assert.Equal("Mendoza Vaca, Carlos", Cliente.nombreVisible cliente)
        Assert.True(Cliente.estaActivo cliente)
        Assert.Equal(EstadoCliente.Activo, cliente.Estado)
        Assert.Equal(Some "+591 76543210", cliente.Telefono)
        Assert.Equal(Some "carlos@vivero.bo", cliente.Email)
        Assert.Equal(Some "Av. Blanco Galindo Km 5", cliente.Direccion)
        match cliente.Tipo with
        | TipoCliente.Natural p -> Assert.Equal("Carlos", p.Nombres)
        | TipoCliente.Juridica _ -> Assert.Fail("Se esperaba cliente tipo Natural")
    | Error err ->
        failwithf "Se esperaba Ok, se obtuvo: %A" err

[<Fact>]
let ``// RF08 | CU-17 | RN18 - Crear Cliente Persona Juridica con representante opcional`` () =
    let rs = match RazonSocial.crear "Mundo Verde S.A." with Ok r -> r | Error e -> failwithf "%A" e
    let nit = match NIT.crear "1002345021" with Ok n -> n | Error e -> failwithf "%A" e

    let ciRep = match CI.crear "5544332" None (Some LP) with Ok c -> c | Error e -> failwithf "%A" e
    let rep = match Persona.crear "Ana" (Some "Gómez") None ciRep None None with Ok p -> p | Error e -> failwithf "%A" e

    match Cliente.crearJuridica rs nit (Some rep) (Some "44234567") (Some "contacto@mundoverde.com") (Some "Calle Junin 123") with
    | Ok cliente ->
        Assert.Equal("Mundo Verde S.A.", Cliente.nombreVisible cliente)
        Assert.True(Cliente.estaActivo cliente)
        Assert.Equal(EstadoCliente.Activo, cliente.Estado)
        Assert.Equal(Some "44234567", cliente.Telefono)
        Assert.Equal(Some "contacto@mundoverde.com", cliente.Email)
        Assert.Equal(Some "Calle Junin 123", cliente.Direccion)
        match cliente.Tipo with
        | TipoCliente.Juridica (r, n, repOpt) ->
            Assert.Equal("Mundo Verde S.A.", RazonSocial.valor r)
            Assert.Equal("1002345021", NIT.valor n)
            Assert.True(repOpt.IsSome)
            Assert.Equal("Gómez, Ana", repOpt.Value.NombreCompleto)
        | TipoCliente.Natural _ -> Assert.Fail("Se esperaba cliente tipo Juridica")
    | Error err ->
        failwithf "Se esperaba Ok, se obtuvo: %A" err

[<Fact>]
let ``// RF08 | CU-17 | RN18 - Crear Cliente Persona Juridica valida telefono corporativo si es invalido`` () =
    let rs = match RazonSocial.crear "Agro Bolivia Ltda." with Ok r -> r | Error e -> failwithf "%A" e
    let nit = match NIT.crear "200300400" with Ok n -> n | Error e -> failwithf "%A" e

    match Cliente.crearJuridica rs nit None (Some "tel-invalido-letras") None None with
    | Error (LetrasNoPermitidas _) -> ()
    | res -> failwithf "Se esperaba LetrasNoPermitidas, se obtuvo: %A" res

[<Fact>]
let ``// RF08 | CU-17 | RN18 - EstadoCliente parseo desde texto y conversion a texto`` () =
    Assert.Equal("Activo", EstadoCliente.aTexto EstadoCliente.Activo)
    Assert.Equal("Inactivo", EstadoCliente.aTexto EstadoCliente.Inactivo)
    Assert.Equal(Ok EstadoCliente.Activo, EstadoCliente.desdeTexto "activo")
    Assert.Equal(Ok EstadoCliente.Activo, EstadoCliente.desdeTexto "Activo")
    Assert.Equal(Ok EstadoCliente.Inactivo, EstadoCliente.desdeTexto "inactivo")
    match EstadoCliente.desdeTexto "suspendido" with
    | Error (ValorRequerido _) -> ()
    | res -> failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res

[<Fact>]
let ``// RF08 | CU-17 | RN18 - ClientService crearClienteNatural retorna DTO con datos completos`` () =
    async {
        let fakeInsertar (c: Cliente) = async { return Ok () }
        let req: CrearClienteNaturalRequest =
            { Nombres = "Roberto"
              ApellidoPaterno = Some "Fernández"
              ApellidoMaterno = Some "Quiroga"
              CiNumero = "7891234"
              CiComplemento = None
              CiExtension = Some "SC"
              Telefono = Some "+591 71002233"
              Email = Some "roberto.fq@gmail.com"
              Direccion = Some "Calle Beni #45" }

        let! res = ClientService.crearClienteNatural fakeInsertar req
        match res with
        | Ok dto ->
            Assert.Equal("Natural", dto.Tipo)
            Assert.Equal("Fernández Quiroga, Roberto", dto.NombreVisible)
            Assert.Equal(Some "+591 71002233", dto.Telefono)
            Assert.Equal(Some "roberto.fq@gmail.com", dto.Email)
            Assert.Equal(Some "Calle Beni #45", dto.Direccion)
            Assert.Equal("Activo", dto.Estado)
            Assert.True(dto.Persona.IsSome)
            let p = dto.Persona.Value
            Assert.Equal("7891234 SC", p.CiFormateado)
        | Error err ->
            failwithf "Se esperaba Ok, se obtuvo error: %s" err
    } |> Async.RunSynchronously

[<Fact>]
let ``// RF08 | CU-17 | RN18 - ClientService crearClienteNatural rechaza CI no numerico`` () =
    async {
        let fakeInsertar (c: Cliente) = async { return Ok () }
        let req: CrearClienteNaturalRequest =
            { Nombres = "Roberto"
              ApellidoPaterno = Some "Fernández"
              ApellidoMaterno = None
              CiNumero = "7891ABC"
              CiComplemento = None
              CiExtension = None
              Telefono = None
              Email = None
              Direccion = None }

        let! res = ClientService.crearClienteNatural fakeInsertar req
        match res with
        | Error msg ->
            Assert.Contains("dígitos", msg)
        | Ok _ ->
            failwith "Se esperaba error por CI inválido"
    } |> Async.RunSynchronously

[<Fact>]
let ``// RF08 | CU-17 | RN18 - ClientService crearClienteJuridica retorna DTO con RazonSocial y NIT`` () =
    async {
        let fakeInsertar (c: Cliente) = async { return Ok () }
        let req: CrearClienteJuridicaRequest =
            { RazonSocial = "Semillas del Oriente S.A."
              Nit = "1029384756"
              Representante = None
              Telefono = Some "33445566"
              Email = Some "ventas@semillasoriente.bo"
              Direccion = Some "Parque Industrial Manzana 12" }

        let! res = ClientService.crearClienteJuridica fakeInsertar req
        match res with
        | Ok dto ->
            Assert.Equal("Juridica", dto.Tipo)
            Assert.Equal("Semillas del Oriente S.A.", dto.NombreVisible)
            Assert.Equal(Some "Semillas del Oriente S.A.", dto.RazonSocial)
            Assert.Equal(Some "1029384756", dto.Nit)
            Assert.True(dto.Persona.IsNone)
            Assert.True(dto.Representante.IsNone)
            Assert.Equal("Activo", dto.Estado)
        | Error err ->
            failwithf "Se esperaba Ok, se obtuvo: %s" err
    } |> Async.RunSynchronously

[<Fact>]
let ``// RF08 | CU-17 | RN18 - ClientService buscarClientes filtra delegando al repositorio y formatea DTOs`` () =
    async {
        let ci = match CI.crear "1234567" None (Some LP) with Ok c -> c | Error e -> failwithf "%A" e
        let p = match Persona.crear "Juan" (Some "Pérez") None ci None None with Ok p -> p | Error e -> failwithf "%A" e
        let cl = match Cliente.crearNatural p None None None with Ok c -> c | Error e -> failwithf "%A" e

        let fakeBuscar (termino: string) = async {
            if termino.Contains("perez", StringComparison.OrdinalIgnoreCase) then
                return [ cl ]
            else
                return []
        }

        let! encontrados = ClientService.buscarClientes fakeBuscar "perez"
        Assert.Single(encontrados) |> ignore
        Assert.Equal("Pérez, Juan", encontrados.Head.NombreVisible)

        let! vacios = ClientService.buscarClientes fakeBuscar "inexistente"
        Assert.Empty(vacios)
    } |> Async.RunSynchronously

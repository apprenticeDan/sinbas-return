namespace Sinbas.Domain

type EstadoEmpleado =
    | Activo
    | Inactivo

type Empleado =
    { Id: EmpleadoId
      NombreCompleto: string
      Estado: EstadoEmpleado }

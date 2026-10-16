export interface PersonaDto {
  nombres: string;
  apellidoPaterno?: string;
  apellidoMaterno?: string;
  nombreCompleto: string;
  ciNumero: string;
  ciComplemento?: string;
  ciExtension?: string;
  ciFormateado: string;
  telefono?: string;
  email?: string;
}

export interface ClienteDto {
  id: string;
  tipo: 'Natural' | 'Juridica';
  nombreVisible: string;
  persona?: PersonaDto;
  razonSocial?: string;
  nit?: string;
  representante?: PersonaDto;
  telefono?: string;
  email?: string;
  direccion?: string;
  estado: string;
}

export interface CrearClienteNaturalPayload {
  nombres: string;
  apellidoPaterno?: string;
  apellidoMaterno?: string;
  ciNumero: string;
  ciComplemento?: string;
  ciExtension?: string;
  telefono?: string;
  email?: string;
  direccion?: string;
}

export interface RepresentantePayload {
  nombres: string;
  apellidoPaterno?: string;
  apellidoMaterno?: string;
  ciNumero: string;
  ciComplemento?: string;
  ciExtension?: string;
  telefono?: string;
  email?: string;
}

export interface CrearClienteJuridicaPayload {
  razonSocial: string;
  nit: string;
  representante?: RepresentantePayload;
  telefono?: string;
  email?: string;
  direccion?: string;
}

export interface ActualizarClientePayload {
  tipo?: 'Natural' | 'Juridica';
  nombres?: string;
  apellidoPaterno?: string;
  apellidoMaterno?: string;
  ciNumero?: string;
  ciComplemento?: string;
  ciExtension?: string;
  razonSocial?: string;
  nit?: string;
  representante?: RepresentantePayload;
  telefono?: string;
  email?: string;
  direccion?: string;
  estado?: string;
}

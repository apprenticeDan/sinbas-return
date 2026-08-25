import { httpClient } from './HttpClient';

export interface LoginRequest {
  nombreUsuarioRaw: string;
  contrasena: string;
}

export interface LoginResponse {
  token: string;
  nombreUsuario: string;
  roles: string[];
}

export const ApiAuthGateway = {
  login: (data: LoginRequest) =>
    httpClient<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
};

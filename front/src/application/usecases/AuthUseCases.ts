import { ApiAuthGateway, LoginRequest } from '../../infrastructure/api/ApiAuthGateway';

export const AuthUseCases = {
  login: async (request: LoginRequest) => {
    const response = await ApiAuthGateway.login(request);
    return response;
  },
};

import { httpClient } from './HttpClient';
import { Product, CreateProductPayload, AssignPricePayload } from '../../domain/models/Product';

export const ApiCatalogGateway = {
  async getProducts(category?: string, state?: string): Promise<Product[]> {
    const params = new URLSearchParams();
    if (category) params.append('categoria', category);
    if (state) params.append('estado', state);
    const queryString = params.toString() ? `?${params.toString()}` : '';
    return httpClient<Product[]>(`/catalogo/productos${queryString}`);
  },

  async createProduct(payload: CreateProductPayload): Promise<Product> {
    return httpClient<Product>('/catalogo/productos', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async assignPrice(payload: AssignPricePayload): Promise<Product> {
    return httpClient<Product>(`/catalogo/productos/${payload.productoId}/precio`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },
};

import { ApiCatalogGateway } from '../../infrastructure/api/ApiCatalogGateway';
import { Product, CreateProductPayload, AssignPricePayload } from '../../domain/models/Product';

export const CatalogUseCases = {
  async fetchCatalog(category?: string, state?: string): Promise<Product[]> {
    return ApiCatalogGateway.getProducts(category, state);
  },

  async createNewProduct(payload: CreateProductPayload): Promise<Product> {
    return ApiCatalogGateway.createProduct(payload);
  },

  async assignOfficialPrice(payload: AssignPricePayload): Promise<Product> {
    return ApiCatalogGateway.assignPrice(payload);
  },
};

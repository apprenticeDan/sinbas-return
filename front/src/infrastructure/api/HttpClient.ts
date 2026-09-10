const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:8080/api';

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

export async function httpClient<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const token = localStorage.getItem('sinbas_token');

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let errorMsg = `Error ${response.status}: ${response.statusText}`;
    try {
      const errData = await response.json();
      if (errData.error) {
        errorMsg = errData.detalle ? `${errData.error} (${errData.detalle})` : errData.error;
      }
    } catch (_) {}
    throw new ApiError(response.status, errorMsg);
  }

  if (response.status === 204) {
    return {} as T;
  }

  const text = await response.text();
  if (!text || text.trim().length === 0) {
    return {} as T;
  }

  try {
    return JSON.parse(text);
  } catch (_) {
    return {} as T;
  }
}

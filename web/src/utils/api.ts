import { ApiResponse } from '@/types';

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public code?: string
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export async function apiRequest<T = any>(
  url: string,
  options: RequestInit = {}
): Promise<T> {
  const response = await fetch(url, {
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
    ...options,
  });

  const data: ApiResponse<T> = await response.json();

  if (!response.ok || !data.success) {
    throw new ApiError(
      data.error || 'Unknown error occurred',
      response.status
    );
  }

  return data.data as T;
}

export const api = {
  get: <T>(url: string, options?: RequestInit) =>
    apiRequest<T>(url, { method: 'GET', ...options }),
  
  post: <T>(url: string, body?: any, options?: RequestInit) =>
    apiRequest<T>(url, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
      ...options,
    }),
  
  put: <T>(url: string, body?: any, options?: RequestInit) =>
    apiRequest<T>(url, {
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
      ...options,
    }),
  
  delete: <T>(url: string, options?: RequestInit) =>
    apiRequest<T>(url, { method: 'DELETE', ...options }),
};
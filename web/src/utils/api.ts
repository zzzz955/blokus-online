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

  let data: ApiResponse<T>;
  const responseText = await response.text();
  
  try {
    data = JSON.parse(responseText);
  } catch (parseError) {
    // HTML 응답인 경우 (404, 500 등)
    console.error('API Response parsing failed:', {
      status: response.status,
      statusText: response.statusText,
      url,
      responseText: responseText.substring(0, 200) + '...'
    });
    throw new ApiError(
      `서버 응답을 파싱할 수 없습니다 (${response.status}: ${response.statusText})`,
      response.status
    );
  }

  if (!response.ok || !data.success) {
    throw new ApiError(
      data.error || 'Unknown error occurred',
      response.status
    );
  }

  return data.data as T;
}

export async function apiRequestFull<T = any>(
  url: string,
  options: RequestInit = {}
): Promise<ApiResponse<T>> {
  const response = await fetch(url, {
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
    ...options,
  });

  let data: ApiResponse<T>;
  const responseText = await response.text();
  
  try {
    data = JSON.parse(responseText);
  } catch (parseError) {
    // HTML 응답인 경우 (404, 500 등)
    console.error('API Response parsing failed:', {
      status: response.status,
      statusText: response.statusText,
      url,
      responseText: responseText.substring(0, 200) + '...'
    });
    throw new ApiError(
      `서버 응답을 파싱할 수 없습니다 (${response.status}: ${response.statusText})`,
      response.status
    );
  }

  if (!response.ok || !data.success) {
    throw new ApiError(
      data.error || 'Unknown error occurred',
      response.status
    );
  }

  return data;
}

export const api = {
  get: <T>(url: string, options?: RequestInit) =>
    apiRequest<T>(url, { method: 'GET', ...options }),
  
  getFull: <T>(url: string, options?: RequestInit) =>
    apiRequestFull<T>(url, { method: 'GET', ...options }),
  
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
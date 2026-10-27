import axios, { AxiosError } from 'axios';
import type { ProblemDetails } from '@/types/api';
import { getIdToken } from './firebase';

/** Error thrown for every failed API call, normalised from RFC 7807 problem details. */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly fieldErrors: Record<string, string[]>;

  constructor(status: number, problem: ProblemDetails | undefined, fallback: string) {
    super(problem?.title ?? fallback);
    this.status = status;
    this.code = problem?.code ?? (status === 0 ? 'network' : 'unknown');
    this.fieldErrors = problem?.errors ?? {};
  }

  /** First message for a field (keys are camelCase paths, e.g. "address.city"). */
  field(name: string): string | undefined {
    return this.fieldErrors[name]?.[0];
  }
}

export const http = axios.create({
  baseURL: (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '',
  timeout: 30_000,
});

// Attach the Firebase ID token; the API derives identity and role from it server-side.
http.interceptors.request.use(async (request) => {
  const token = await getIdToken();
  if (token) request.headers.Authorization = `Bearer ${token}`;
  return request;
});

http.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ProblemDetails>) => {
    if (error.response) {
      throw new ApiError(error.response.status, error.response.data, 'Something went wrong. Please try again.');
    }
    throw new ApiError(0, undefined, 'We could not reach the server. Check your connection and try again.');
  },
);

export const get = async <T>(url: string, params?: unknown) => (await http.get<T>(url, { params, paramsSerializer: { indexes: null } })).data;
export const post = async <T>(url: string, body?: unknown) => (await http.post<T>(url, body)).data;
export const put = async <T>(url: string, body?: unknown) => (await http.put<T>(url, body)).data;
export const patch = async <T>(url: string, body?: unknown) => (await http.patch<T>(url, body)).data;
export const del = async <T>(url: string) => (await http.delete<T>(url)).data;

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message;
  if (error instanceof Error) return error.message;
  return 'Something went wrong. Please try again.';
}

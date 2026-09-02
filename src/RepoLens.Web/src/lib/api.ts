import { z } from 'zod'

/**
 * Backend DTO schemas (camelCase — ASP.NET Core default JSON naming).
 * Validated at the network boundary so unexpected server shapes degrade into
 * typed errors instead of rendering garbage.
 */

const rateLimitSchema = z.object({
  limit: z.number(),
  remaining: z.number(),
  resetAtUtc: z.string().nullable(),
})

const pageMetadataSchema = z.object({
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number().nullable(),
  itemCount: z.number(),
})

export const repositoryListItemSchema = z.object({
  id: z.number(),
  gitHubId: z.number(),
  owner: z.string(),
  name: z.string(),
  fullName: z.string(),
  description: z.string().nullable(),
  htmlUrl: z.string(),
  defaultBranch: z.string().nullable(),
  primaryLanguage: z.string().nullable(),
  stars: z.number(),
  forks: z.number(),
  openIssues: z.number(),
  isArchived: z.boolean(),
  isFork: z.boolean(),
  licenseSpdx: z.string().nullable(),
  createdAtUtc: z.string(),
  updatedAtUtc: z.string(),
  pushedAtUtc: z.string().nullable(),
})

export const discoveryResponseSchema = z.object({
  query: z.string(),
  pagination: pageMetadataSchema,
  rateLimit: rateLimitSchema.nullable(),
  items: z.array(repositoryListItemSchema),
})

export type RepositoryListItem = z.infer<typeof repositoryListItemSchema>
export type DiscoveryResponse = z.infer<typeof discoveryResponseSchema>

/** Error envelope returned by the API exception handler (ProblemDetails + code). */
const apiErrorSchema = z.object({
  status: z.number().optional(),
  title: z.string().optional(),
  detail: z.string().optional(),
  code: z.string().optional(),
})

export class ApiError extends Error {
  readonly status: number
  readonly code?: string

  constructor(message: string, status: number, code?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

export interface RequestInitFragment {
  method?: string
  headers?: Record<string, string>
  body?: string
}

async function parseJson(response: Response): Promise<unknown> {
  const text = await response.text()
  if (!text) {
    return null
  }
  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

/**
 * Calls a relative backend URL and validates the payload with the given schema.
 * Non-GET requests are supported via `init` (method/headers/body).
 */
export async function getJson<T>(
  path: string,
  schema: z.ZodType<T>,
  signal?: AbortSignal,
  init?: RequestInitFragment,
): Promise<T> {
  let response: Response
  try {
    response = await fetch(path, { signal, ...init })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    throw new ApiError('Backend is unreachable.', 0)
  }

  if (!response.ok) {
    const body = await parseJson(response)
    const parsed = apiErrorSchema.safeParse(body)
    const code = parsed.success ? parsed.data.code : undefined
    const detail = parsed.success ? parsed.data.detail : undefined
    throw new ApiError(detail ?? `Request failed with status ${response.status}.`, response.status, code)
  }

  const body = await parseJson(response)
  const parsed = schema.safeParse(body)
  if (!parsed.success) {
    throw new ApiError('Backend returned an unexpected response shape.', response.status)
  }
  return parsed.data
}

export interface SearchParams {
  query: string
  page: number
  pageSize?: number
}

export function searchRepositories(
  { query, page, pageSize = 30 }: SearchParams,
  signal?: AbortSignal,
): Promise<DiscoveryResponse> {
  const params = new URLSearchParams({ q: query, page: String(page), perPage: String(pageSize) })
  return getJson(`/api/discovery/repositories?${params}`, discoveryResponseSchema, signal)
}

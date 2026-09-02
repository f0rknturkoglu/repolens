import { getJson } from './api'
import { z } from 'zod'

/** GET /api/search/repositories response. */

export const repositorySearchResponseSchema = z.object({
  query: z.string(),
  method: z.enum(['Keyword', 'Hybrid']),
  vectorFallbackReason: z.string().nullable(),
  filters: z.object({
    language: z.string().nullable(),
    archived: z.boolean().nullable(),
    minStars: z.number().nullable(),
  }),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
  items: z.array(
    z.object({
      id: z.number(),
      gitHubId: z.number(),
      owner: z.string(),
      name: z.string(),
      fullName: z.string(),
      description: z.string().nullable(),
      htmlUrl: z.string(),
      primaryLanguage: z.string().nullable(),
      stars: z.number(),
      forks: z.number(),
      openIssues: z.number(),
      isArchived: z.boolean(),
      licenseSpdx: z.string().nullable(),
      updatedAtUtc: z.string(),
      score: z.number(),
    }),
  ),
})

export type RepositorySearchResponse = z.infer<typeof repositorySearchResponseSchema>

export interface SearchFiltersInput {
  language?: string
  archived?: boolean
  minStars?: number
}

export function searchRepoLens(
  query: string,
  filters: SearchFiltersInput,
  page: number,
  signal?: AbortSignal,
): Promise<RepositorySearchResponse> {
  const params = new URLSearchParams({ q: query, page: String(page), perPage: '20' })
  if (filters.language) params.set('language', filters.language)
  if (filters.archived !== undefined) params.set('archived', String(filters.archived))
  if (filters.minStars !== undefined && filters.minStars > 0) params.set('minStars', String(filters.minStars))
  return getJson(`/api/search/repositories?${params}`, repositorySearchResponseSchema, signal)
}

/** GET /api/repositories/{id}/similar response. */

const similarItemSchema = z.object({
  id: z.number(),
  fullName: z.string(),
  description: z.string().nullable(),
  primaryLanguage: z.string().nullable(),
  stars: z.number(),
  isArchived: z.boolean(),
  similarity: z.number(),
  evidence: z.array(z.string()),
})

export const similarRepositoriesResponseSchema = z.object({
  repositoryId: z.number(),
  status: z.string(),
  reason: z.string().nullable(),
  items: z.array(similarItemSchema),
})

export type SimilarRepositoriesResponse = z.infer<typeof similarRepositoriesResponseSchema>

export function fetchSimilarRepositories(
  repositoryId: number | string,
  signal?: AbortSignal,
): Promise<SimilarRepositoriesResponse> {
  return getJson(`/api/repositories/${repositoryId}/similar?limit=10`, similarRepositoriesResponseSchema, signal)
}

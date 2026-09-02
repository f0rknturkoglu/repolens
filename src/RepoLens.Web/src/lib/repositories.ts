import { getJson } from './api'
import { z } from 'zod'

/** GET /api/repositories/{id} response (camelCase; ASP.NET Core default). */

const enrichmentStatusSchema = z.object({
  status: z.string(),
  attempts: z.number(),
  lastErrorCategory: z.string().nullable(),
  lastError: z.string().nullable(),
  nextAttemptAtUtc: z.string().nullable(),
})

const languageShareSchema = z.object({
  language: z.string(),
  bytes: z.number(),
  percentage: z.number(),
})

const readmeInfoSchema = z.object({
  present: z.boolean(),
  text: z.string().nullable(),
  truncated: z.boolean(),
  fetchedAtUtc: z.string().nullable(),
})

export const repositoryDetailSchema = z.object({
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
  discoveredAtUtc: z.string(),
  enrichedAtUtc: z.string().nullable(),
  topics: z.array(z.string()),
  languages: z.array(languageShareSchema),
  readme: readmeInfoSchema.nullable(),
  enrichment: enrichmentStatusSchema,
})

export type RepositoryDetail = z.infer<typeof repositoryDetailSchema>
export type EnrichmentStatus = RepositoryDetail['enrichment']

export function fetchRepositoryDetail(repositoryId: number | string, signal?: AbortSignal) {
  return getJson(`/api/repositories/${repositoryId}`, repositoryDetailSchema, signal)
}

/** POST /api/repositories/{id}/refresh — manual enrichment trigger. */
export async function refreshRepository(repositoryId: number | string): Promise<void> {
  const response = await fetch(`/api/repositories/${repositoryId}/refresh`, { method: 'POST' })
  if (response.ok) {
    return
  }
  if (response.status === 409) {
    throw new RefreshError('An enrichment job is already pending or running for this repository.', 409)
  }
  if (response.status === 429) {
    throw new RefreshError('This repository was refreshed very recently. Try again in a few minutes.', 429)
  }
  if (response.status === 404) {
    throw new RefreshError('Repository not found.', 404)
  }
  throw new RefreshError('Refresh failed.', response.status)
}

export class RefreshError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'RefreshError'
    this.status = status
  }
}

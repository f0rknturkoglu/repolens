import { getJson } from './api'
import { z } from 'zod'

const authStatusSchema = z.object({
  enabled: z.boolean(),
  user: z
    .object({
      userId: z.number(),
      login: z.string(),
      name: z.string().nullable(),
      avatarUrl: z.string().nullable(),
    })
    .nullable(),
})

const historyEntrySchema = z.object({
  id: z.number(),
  kind: z.string(),
  referenceId: z.number(),
  title: z.string(),
  status: z.string(),
  version: z.string(),
  createdAtUtc: z.string(),
})

export type AuthStatus = z.infer<typeof authStatusSchema>
export type HistoryEntry = z.infer<typeof historyEntrySchema>

export function fetchAuthStatus(signal?: AbortSignal): Promise<AuthStatus> {
  return getJson('/api/auth/status', authStatusSchema, signal)
}

export function fetchHistory(signal?: AbortSignal): Promise<HistoryEntry[]> {
  return getJson('/api/auth/me/history', z.array(historyEntrySchema), signal)
}

export function historyPath(entry: HistoryEntry): string {
  switch (entry.kind) {
    case 'ecosystem':
      return '/ecosystem?analysis=' + entry.referenceId
    case 'portfolio':
      return '/portfolio?username=' + encodeURIComponent(entry.title)
    case 'idea':
      return '/validate?id=' + entry.referenceId
    case 'recommendation':
      return '/recommend?id=' + entry.referenceId
    default:
      return '/'
  }
}

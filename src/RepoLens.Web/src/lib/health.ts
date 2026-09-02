import { z } from 'zod'

/**
 * ASP.NET Core Health Checks default text payload returned by GET /health.
 * Validated at the network boundary with Zod so an unexpected server response
 * degrades to "unavailable" instead of being rendered as healthy.
 */
const healthResponseSchema = z.enum(['Healthy', 'Degraded', 'Unhealthy'])

export type ApiHealthState = 'checking' | 'healthy' | 'unavailable'

/**
 * Calls the backend /health endpoint. In development Vite proxies /health to the
 * ASP.NET Core API (localhost:5190); in the Docker stack nginx does the same.
 * A non-2xx response, an unknown payload, or a network error all mean
 * "unavailable" from the UI's perspective.
 */
export async function fetchApiHealth(signal?: AbortSignal): Promise<ApiHealthState> {
  const response = await fetch('/health', { signal })
  if (!response.ok) {
    return 'unavailable'
  }

  const parsed = healthResponseSchema.safeParse(await response.text())
  return parsed.success && parsed.data === 'Healthy' ? 'healthy' : 'unavailable'
}

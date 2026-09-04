import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    // Development proxy: the browser calls relative /health (and later /api/*),
    // Vite forwards it to the ASP.NET Core API running on the host.
    proxy: {
      '/health': 'http://localhost:5190',
      '/api': 'http://localhost:5190',
    },
  },
})

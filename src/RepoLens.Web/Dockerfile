# Build stage: production build of the React SPA
FROM node:24-alpine AS build
WORKDIR /app

# Install dependencies first so the layer is cached until package files change.
COPY src/RepoLens.Web/package.json src/RepoLens.Web/package-lock.json ./
RUN npm ci

COPY src/RepoLens.Web/ .
RUN npm run build

# Runtime stage: static files served by nginx, which also proxies /health and
# /api/* to the api service of the compose stack.
FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY src/RepoLens.Web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80

# syntax=docker/dockerfile:1

# Image versions are pinned, so a rebuild gives the same base images. Bump them on purpose, not by
# accident: node 24.22 satisfies package.json "engines" (>=24.12), nginx-unprivileged 1.30 is the nginx
# build that runs as a non-root user on port 8080.
ARG NODE_IMAGE=node:24.22-alpine
ARG NGINX_IMAGE=nginxinc/nginx-unprivileged:1.30-alpine

# Build stage: Node compiles the app. Only dist/ leaves this stage.
FROM ${NODE_IMAGE} AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# Runtime stage: nginx as a non-root user on port 8080. The unprivileged image is the official nginx
# build for this: it needs no root to bind a port and no root-owned runtime directory.
FROM ${NGINX_IMAGE} AS web
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY nginx-security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1

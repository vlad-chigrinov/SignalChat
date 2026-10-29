# syntax=docker/dockerfile:1

# Build stage: Node compiles the app. Only dist/ leaves this stage.
# Node 24 is the lowest major in package.json "engines" that is still supported.
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# Runtime stage: nginx as a non-root user on port 8080. The unprivileged image is the official nginx
# build for this: it needs no root to bind a port and no root-owned runtime directory.
FROM nginxinc/nginx-unprivileged:stable-alpine AS web
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY nginx-security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1

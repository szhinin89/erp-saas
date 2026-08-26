# Frontend (React/Vite) — imagen production-like para docker-compose.localprod.yml
# Build context: raíz del repo (npm run build invoca ../tools/ci/run-platform-guard.mjs,
# por eso el build necesita ver frontend/ y tools/ en el mismo árbol).
#
#   docker compose -f docker-compose.localprod.yml build   (desde la raíz del repo)

# ---- Stage 1: build ----
FROM node:22-alpine AS build
WORKDIR /repo

COPY frontend/package.json frontend/package-lock.json frontend/
RUN --mount=type=cache,target=/root/.npm \
    cd frontend && npm ci

COPY tools/ tools/
COPY frontend/ frontend/

ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL

RUN cd frontend && npm run build

# ---- Stage 2: nginx estático ----
FROM nginx:1.27-alpine AS runtime

COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /repo/frontend/dist /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=10s --timeout=3s --retries=5 \
    CMD wget -q --spider http://127.0.0.1:80/ || exit 1

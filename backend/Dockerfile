# ERP.API — imagen production-like para docker-compose.localprod.yml
# Build context: backend/  (docker compose -f docker-compose.localprod.yml build en la raíz del repo)
#
# Esta imagen NO incluye PostgreSQL ni Redis: se conecta a los contenedores
# existentes (postgreszh / erp-saas-redis) vía la red Docker externa erp-saas_default.
# Ver docs/DOCKER_LOCAL_PROD.md.

# ---- Stage 1: restore + publish ----
# Versión exacta fijada por backend/global.json (rollForward: disable).
FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
WORKDIR /src

# Copiar solo los .csproj primero (cache de `dotnet restore` entre builds).
COPY src/ERP.API/ERP.API.csproj src/ERP.API/
COPY src/ERP.Application/ERP.Application.csproj src/ERP.Application/
COPY src/ERP.Domain/ERP.Domain.csproj src/ERP.Domain/
COPY src/ERP.Infrastructure/ERP.Infrastructure.csproj src/ERP.Infrastructure/

RUN dotnet restore src/ERP.API/ERP.API.csproj

# Copiar el resto del código fuente de los proyectos referenciados por ERP.API.
COPY src/ERP.API/ src/ERP.API/
COPY src/ERP.Application/ src/ERP.Application/
COPY src/ERP.Domain/ src/ERP.Domain/
COPY src/ERP.Infrastructure/ src/ERP.Infrastructure/

RUN dotnet publish src/ERP.API/ERP.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- Stage 2: runtime ----
# Ubuntu/noble (glibc, familia Debian) — no Alpine/musl. Para .NET 10, Microsoft ya
# no publica tag "bookworm-slim" (mcr.microsoft.com/dotnet/aspnet:10.0-bookworm-slim
# no existe); "noble" (Ubuntu 24.04) es la imagen glibc/apt-based equivalente y es el
# tag base que usa "10.0"/"10.0.203" sin sufijo. QuestPDF/SkiaSharp requieren
# librerías nativas de render que la imagen Alpine no trae de forma estable —
# causaban crash nativo (SIGSEGV, exit 139) en el arranque bajo Coolify/Vultr.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        tzdata \
        fontconfig \
        libfontconfig1 \
        libfreetype6 \
        libharfbuzz0b \
        libjpeg-turbo8 \
        libpng16-16 \
        libwebp7 \
        libx11-6 \
        libxext6 \
        libxrender1 \
        libxcb1 \
        zlib1g \
        curl \
        wget \
        tini \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

# Migraciones (db.Database.MigrateAsync()) se aplican automáticamente en el
# arranque de ERP.API contra la base de datos existente — no se ejecuta ningún
# comando de inicialización destructivo desde esta imagen.
#
# tini como PID 1: sin un init real, dotnet corre como PID 1 y el manejador de
# señales del runtime (createdump/SIGABRT en excepciones no controladas) puede
# reportar un exit code distinto al real (observado: 139/SIGSEGV en vez del
# 134/SIGABRT correcto) — Coolify/Docker interpretan eso como crash nativo y
# reinician el contenedor en loop. tini reenvía señales y reaporta el exit
# code real del proceso hijo.
ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "ERP.API.dll"]

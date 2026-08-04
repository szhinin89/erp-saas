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
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

ENV DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

# Migraciones (db.Database.MigrateAsync()) se aplican automáticamente en el
# arranque de ERP.API contra la base de datos existente — no se ejecuta ningún
# comando de inicialización destructivo desde esta imagen.
ENTRYPOINT ["dotnet", "ERP.API.dll"]

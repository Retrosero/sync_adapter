# Tum source kodunu Docker build context root'una kopyala
# Build context = server/ — kaynak dosyalar /src altinda
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 1) Source kodun tamamini /app/src/ altina kopyala (buyuk ama temiz)
COPY src/ /app/src/

# 2) Project file'larini ayri kopyala (Docker layer caching icin)
COPY src/FieldOps.Api/FieldOps.Api.csproj /app/src/FieldOps.Api/FieldOps.Api.csproj
COPY src/FieldOps.Application/FieldOps.Application.csproj /app/src/FieldOps.Application/FieldOps.Application.csproj
COPY src/FieldOps.Infrastructure/FieldOps.Infrastructure.csproj /app/src/FieldOps.Infrastructure/FieldOps.Infrastructure.csproj
COPY src/FieldOps.Domain/FieldOps.Domain.csproj /app/src/FieldOps.Domain/FieldOps.Domain.csproj
COPY src/FieldOps.Contracts/FieldOps.Contracts.csproj /app/src/FieldOps.Contracts/FieldOps.Contracts.csproj

# 3) Restore + publish (API projesi dependency zinciriyle birlikte)
RUN dotnet restore /app/src/FieldOps.Api/FieldOps.Api.csproj && \
    dotnet publish /app/src/FieldOps.Api/FieldOps.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Build arg'leri runtime ENV'ye aktar
ARG POSTGRES_HOST=localhost
ARG POSTGRES_PORT=5432
ARG POSTGRES_DB=fieldops
ARG POSTGRES_USER=fieldops
ARG POSTGRES_PASSWORD=

ENV POSTGRES_HOST=${POSTGRES_HOST}
ENV POSTGRES_PORT=${POSTGRES_PORT}
ENV POSTGRES_DB=${POSTGRES_DB}
ENV POSTGRES_USER=${POSTGRES_USER}
ENV POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
ENV ConnectionStrings__Postgres="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};SSL Mode=Prefer"
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
RUN mkdir -p /app/Logs

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "FieldOps.Api.dll"]

# Build arguments for database configuration
# Bunlar "ARG" olarak tanimlanmali — Coolify build-time arg olarak gonderebilir.
# Coolify'da tek tek ayri ayri degisken olarak gonder (connection string degil!)
ARG POSTGRES_HOST=localhost
ARG POSTGRES_PORT=5432
ARG POSTGRES_DB=fieldops
ARG POSTGRES_USER=fieldops
ARG POSTGRES_PASSWORD=

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/FieldOps.Contracts/FieldOps.Contracts.csproj src/FieldOps.Contracts/
COPY src/FieldOps.Domain/FieldOps.Domain.csproj src/FieldOps.Domain/
COPY src/FieldOps.Application/FieldOps.Application.csproj src/FieldOps.Application/
COPY src/FieldOps.Infrastructure/FieldOps.Infrastructure.csproj src/FieldOps.Infrastructure/
COPY src/FieldOps.Api/FieldOps.Api.csproj src/FieldOps.Api/
RUN dotnet restore src/FieldOps.Api/FieldOps.Api.csproj
COPY . .
RUN dotnet publish src/FieldOps.Api/FieldOps.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Build arg'lari runtime ENV'ye aktar
ARG POSTGRES_HOST
ARG POSTGRES_PORT
ARG POSTGRES_DB
ARG POSTGRES_USER
ARG POSTGRES_PASSWORD
ENV POSTGRES_HOST=${POSTGRES_HOST}
ENV POSTGRES_PORT=${POSTGRES_PORT}
ENV POSTGRES_DB=${POSTGRES_DB}
ENV POSTGRES_USER=${POSTGRES_USER}
ENV POSTGRES_PASSWORD=${POSTGRES_PASSWORD}

# Connection string'i runtime'da PARCALARDAN derle — = sorunundan kurtulur
ENV ConnectionStrings__Postgres="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};SSL Mode=Prefer"
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
RUN mkdir -p /app/Logs
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FieldOps.Api.dll"]

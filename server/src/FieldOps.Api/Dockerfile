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
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
RUN mkdir -p /app/Logs
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FieldOps.Api.dll"]

# =============================================================================
# TakeTime Database Migrator - Multi-stage Dockerfile
# =============================================================================
# Build:  docker build -f src/Migrator/TakeTime.Migrator/Dockerfile -t taketime/migrator .
# Run:    docker run --network taketime-network taketime/migrator
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY TakeTime.Microservices.sln ./
COPY src/Migrator/TakeTime.Migrator/TakeTime.Migrator.csproj src/Migrator/TakeTime.Migrator/

RUN dotnet restore src/Migrator/TakeTime.Migrator/TakeTime.Migrator.csproj

COPY src/Migrator/ src/Migrator/

RUN dotnet publish src/Migrator/TakeTime.Migrator/TakeTime.Migrator.csproj \
    -c Release \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS final
WORKDIR /app

RUN apk add --no-cache icu-libs tzdata \
    && cp /usr/share/zoneinfo/Asia/Bangkok /etc/localtime \
    && echo "Asia/Bangkok" > /etc/timezone

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TakeTime.Migrator.dll"]

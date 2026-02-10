# =============================================================================
# TakeTime Reservation API - Multi-stage Dockerfile
# =============================================================================
# Build:  docker build -f src/Services/Reservation/TakeTime.Reservation.API/Dockerfile -t taketime/reservation-api .
# Run:    docker run -p 5001:5001 taketime/reservation-api
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Base runtime image
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 5001

# Install curl for health checks, ICU for globalization, and tzdata
RUN apk add --no-cache curl icu-libs tzdata \
    && cp /usr/share/zoneinfo/Asia/Bangkok /etc/localtime \
    && echo "Asia/Bangkok" > /etc/timezone

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_URLS=http://+:5001

# Create non-root user for security
RUN addgroup -S taketime && adduser -S taketime -G taketime
USER taketime

# ---------------------------------------------------------------------------
# Stage 2: Build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution and project files first for layer caching
COPY TakeTime.Microservices.sln ./
COPY src/Core/TakeTime.Core/TakeTime.Core.csproj src/Core/TakeTime.Core/
COPY src/MultiTenancy/TakeTime.MultiTenancy/TakeTime.MultiTenancy.csproj src/MultiTenancy/TakeTime.MultiTenancy/
COPY src/Identity/TakeTime.Identity/TakeTime.Identity.csproj src/Identity/TakeTime.Identity/
COPY src/Infrastructure/TakeTime.Infrastructure/TakeTime.Infrastructure.csproj src/Infrastructure/TakeTime.Infrastructure/
COPY src/Services/Reservation/TakeTime.Reservation.Domain/TakeTime.Reservation.Domain.csproj src/Services/Reservation/TakeTime.Reservation.Domain/
COPY src/Services/Reservation/TakeTime.Reservation.Application/TakeTime.Reservation.Application.csproj src/Services/Reservation/TakeTime.Reservation.Application/
COPY src/Services/Reservation/TakeTime.Reservation.Infrastructure/TakeTime.Reservation.Infrastructure.csproj src/Services/Reservation/TakeTime.Reservation.Infrastructure/
COPY src/Services/Reservation/TakeTime.Reservation.API/TakeTime.Reservation.API.csproj src/Services/Reservation/TakeTime.Reservation.API/

# Restore dependencies (cached unless .csproj files change)
RUN dotnet restore src/Services/Reservation/TakeTime.Reservation.API/TakeTime.Reservation.API.csproj

# Copy all source code
COPY src/Core/ src/Core/
COPY src/MultiTenancy/ src/MultiTenancy/
COPY src/Identity/ src/Identity/
COPY src/Infrastructure/ src/Infrastructure/
COPY src/Services/Reservation/ src/Services/Reservation/

# Build
RUN dotnet build src/Services/Reservation/TakeTime.Reservation.API/TakeTime.Reservation.API.csproj \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/build

# ---------------------------------------------------------------------------
# Stage 3: Publish
# ---------------------------------------------------------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish src/Services/Reservation/TakeTime.Reservation.API/TakeTime.Reservation.API.csproj \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:PublishTrimmed=false

# ---------------------------------------------------------------------------
# Stage 4: Final runtime image
# ---------------------------------------------------------------------------
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=5 \
    CMD curl -f http://localhost:5001/health || exit 1

ENTRYPOINT ["dotnet", "TakeTime.Reservation.API.dll"]

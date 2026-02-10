# =============================================================================
# TakeTime Payment API - Multi-stage Dockerfile
# =============================================================================
# Build:  docker build -f src/Services/Payment/TakeTime.Payment.API/Dockerfile -t taketime/payment-api .
# Run:    docker run -p 5002:5002 taketime/payment-api
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Base runtime image
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 5002

# Install curl for health checks, ICU for globalization, and tzdata
RUN apk add --no-cache curl icu-libs tzdata \
    && cp /usr/share/zoneinfo/Asia/Bangkok /etc/localtime \
    && echo "Asia/Bangkok" > /etc/timezone

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_URLS=http://+:5002

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
COPY src/Services/Payment/TakeTime.Payment.Domain/TakeTime.Payment.Domain.csproj src/Services/Payment/TakeTime.Payment.Domain/
COPY src/Services/Payment/TakeTime.Payment.Application/TakeTime.Payment.Application.csproj src/Services/Payment/TakeTime.Payment.Application/
COPY src/Services/Payment/TakeTime.Payment.Infrastructure/TakeTime.Payment.Infrastructure.csproj src/Services/Payment/TakeTime.Payment.Infrastructure/
COPY src/Services/Payment/TakeTime.Payment.API/TakeTime.Payment.API.csproj src/Services/Payment/TakeTime.Payment.API/

# Restore dependencies
RUN dotnet restore src/Services/Payment/TakeTime.Payment.API/TakeTime.Payment.API.csproj

# Copy all source code
COPY src/Core/ src/Core/
COPY src/MultiTenancy/ src/MultiTenancy/
COPY src/Identity/ src/Identity/
COPY src/Infrastructure/ src/Infrastructure/
COPY src/Services/Payment/ src/Services/Payment/

# Build
RUN dotnet build src/Services/Payment/TakeTime.Payment.API/TakeTime.Payment.API.csproj \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/build

# ---------------------------------------------------------------------------
# Stage 3: Publish
# ---------------------------------------------------------------------------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish src/Services/Payment/TakeTime.Payment.API/TakeTime.Payment.API.csproj \
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
    CMD curl -f http://localhost:5002/health || exit 1

ENTRYPOINT ["dotnet", "TakeTime.Payment.API.dll"]

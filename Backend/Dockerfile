# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["ChillerPlant.csproj", "./"]
RUN dotnet restore "ChillerPlant.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "ChillerPlant.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "ChillerPlant.csproj" -c Release -o /app/publish \
    --no-restore \
    --self-contained false \
    --runtime linux-x64

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install required dependencies for System.Drawing if needed
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    tzdata \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Set timezone
ENV TZ=Asia/Shanghai
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Create non-root user for security
RUN adduser \
    --disabled-password \
    --gecos "" \
    --home "/app" \
    --shell "/bin/bash" \
    --uid "1000" \
    "appuser"

# Copy published app
COPY --from=publish /app/publish .

# Create data directory for model weights
RUN mkdir -p /app/Data && chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose ports
EXPOSE 8080
EXPOSE 443

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV COMPlus_EnableDiagnostics=0

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "ChillerPlant.dll"]

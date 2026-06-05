# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["BacnetSimulator.csproj", "./"]
RUN dotnet restore "BacnetSimulator.csproj"

COPY . .
RUN dotnet build "BacnetSimulator.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "BacnetSimulator.csproj" -c Release -o /app/publish \
    --no-restore \
    --self-contained false \
    --runtime linux-x64

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

RUN apt-get update && apt-get install -y \
    tzdata \
    curl \
    && rm -rf /var/lib/apt/lists/*

ENV TZ=Asia/Shanghai
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

RUN adduser \
    --disabled-password \
    --gecos "" \
    --home "/app" \
    --shell "/bin/bash" \
    --uid "1000" \
    "appuser"

COPY --from=publish /app/publish .

RUN chown -R appuser:appuser /app
USER appuser

ENTRYPOINT ["dotnet", "BacnetSimulator.dll"]

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/ChillerPlantOptimization.API.csproj", "src/"]
RUN dotnet restore "src/ChillerPlantOptimization.API.csproj"

COPY . .
WORKDIR "/src/src"
RUN dotnet build "ChillerPlantOptimization.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ChillerPlantOptimization.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443
EXPOSE 47808/udp

RUN apk add --no-cache tzdata
ENV TZ=Asia/Shanghai
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

COPY --from=publish /app/publish .
COPY --from=publish /app/publish/models /app/models || true

RUN mkdir -p /app/logs && chmod 755 /app/logs

HEALTHCHECK --interval=30s --timeout=3s --start-period=60s --retries=3 \
    CMD wget -q --spider http://localhost/health || exit 1

USER app
ENTRYPOINT ["dotnet", "ChillerPlantOptimization.API.dll"]

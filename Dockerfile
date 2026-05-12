FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SimulacroExamen.csproj", "."]
RUN dotnet restore SimulacroExamen.csproj
COPY . .
# Publicar solo el proyecto web, no la solución completa (que incluye los tests)
RUN dotnet publish SimulacroExamen.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# PORT es inyectado dinámicamente por Railway; fallback a 8080 para entornos locales
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet SimulacroExamen.dll"]

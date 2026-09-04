# Multi-stage build for the three hosts. Build one host with:
#   docker build --target api    -t coliseum-api:local    .
#   docker build --target worker -t coliseum-worker:local .
#   docker build --target mcp    -t coliseum-mcp:local    .
# Runtime image: chiseled Ubuntu (no shell, no package manager), non-root, read-only friendly (OPS-07).

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer: only the files that influence package resolution, so it is cached until a dependency changes.
COPY Directory.Build.props Directory.Packages.props global.json nuget.config ./
COPY src/Coliseum.Domain/Coliseum.Domain.csproj src/Coliseum.Domain/
COPY src/Coliseum.Contracts/Coliseum.Contracts.csproj src/Coliseum.Contracts/
COPY src/Coliseum.Application/Coliseum.Application.csproj src/Coliseum.Application/
COPY src/Coliseum.Infrastructure.Redis/Coliseum.Infrastructure.Redis.csproj src/Coliseum.Infrastructure.Redis/
COPY src/Coliseum.ServiceDefaults/Coliseum.ServiceDefaults.csproj src/Coliseum.ServiceDefaults/
COPY src/Coliseum.Api/Coliseum.Api.csproj src/Coliseum.Api/
COPY src/Coliseum.Worker/Coliseum.Worker.csproj src/Coliseum.Worker/
COPY src/Coliseum.Mcp/Coliseum.Mcp.csproj src/Coliseum.Mcp/
RUN dotnet restore src/Coliseum.Api/Coliseum.Api.csproj \
 && dotnet restore src/Coliseum.Worker/Coliseum.Worker.csproj \
 && dotnet restore src/Coliseum.Mcp/Coliseum.Mcp.csproj

# Sources (tests, docs and private notes are excluded by .dockerignore).
COPY .editorconfig ./
COPY src/ src/

ARG CONFIGURATION=Release
RUN dotnet publish src/Coliseum.Api/Coliseum.Api.csproj       -c $CONFIGURATION -o /out/api    --no-restore \
 && dotnet publish src/Coliseum.Worker/Coliseum.Worker.csproj -c $CONFIGURATION -o /out/worker --no-restore \
 && dotnet publish src/Coliseum.Mcp/Coliseum.Mcp.csproj       -c $CONFIGURATION -o /out/mcp    --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime-base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
USER $APP_UID

FROM runtime-base AS api
COPY --from=build /out/api .
ENTRYPOINT ["dotnet", "Coliseum.Api.dll"]

FROM runtime-base AS worker
COPY --from=build /out/worker .
ENTRYPOINT ["dotnet", "Coliseum.Worker.dll"]

FROM runtime-base AS mcp
COPY --from=build /out/mcp .
ENTRYPOINT ["dotnet", "Coliseum.Mcp.dll"]

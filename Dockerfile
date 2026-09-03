# Multi-stage build for the three hosts. Build a specific host with:
#   docker build --target api    -t coliseum-api    .
#   docker build --target worker -t coliseum-worker .
#   docker build --target mcp    -t coliseum-mcp    .
# Runtime image: chiseled (distroless-style), non-root, read-only friendly.
#
# STUB (MP-09): stages are declared; the copy/publish steps are filled in when the hosts exist.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# restore layer (solution + props + csproj only, for cache efficiency)
# publish layer per host

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime-base
WORKDIR /app
USER $APP_UID
ENV DOTNET_gcServer=0 ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

FROM runtime-base AS api
# COPY --from=build /out/api .
# ENTRYPOINT ["dotnet", "Coliseum.Api.dll"]

FROM runtime-base AS worker
# COPY --from=build /out/worker .
# ENTRYPOINT ["dotnet", "Coliseum.Worker.dll"]

FROM runtime-base AS mcp
# COPY --from=build /out/mcp .
# ENTRYPOINT ["dotnet", "Coliseum.Mcp.dll"]

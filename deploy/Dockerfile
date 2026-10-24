# The three deploy images (Técnico, deploy section) from one build: deploy/deploy.sh builds the targets
# api, client and smoke in turn, and BuildKit reuses the shared stages, so the solution is restored and
# compiled once per deploy.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
# Only the project files: this layer, with the restored packages in it, is reused until a .csproj
# changes. A new project referenced by these must be added here, or its restore fails loudly.
COPY src/RuinaRPG.Domain/RuinaRPG.Domain.csproj src/RuinaRPG.Domain/
COPY src/RuinaRPG.Contracts/RuinaRPG.Contracts.csproj src/RuinaRPG.Contracts/
COPY src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj src/RuinaRPG.Infrastructure/
COPY src/RuinaRPG.Api/RuinaRPG.Api.csproj src/RuinaRPG.Api/
COPY src/RuinaRPG.Client/RuinaRPG.Client.csproj src/RuinaRPG.Client/
COPY tools/RuinaRPG.Smoke/RuinaRPG.Smoke.csproj tools/RuinaRPG.Smoke/
# The packages stay in the layer, never only in a cache mount: a pruned mount must not break a build
# whose restore layer survived. The mount on NuGet's http-cache only saves the downloads when a .csproj
# changes.
RUN --mount=type=cache,id=ruinarpg-nuget-http,target=/root/.local/share/NuGet/http-cache \
    dotnet restore src/RuinaRPG.Api/RuinaRPG.Api.csproj \
 && dotnet restore src/RuinaRPG.Client/RuinaRPG.Client.csproj \
 && dotnet restore tools/RuinaRPG.Smoke/RuinaRPG.Smoke.csproj

FROM restore AS build
ARG GIT_SHA=dev
COPY src/ src/
COPY tools/RuinaRPG.Smoke/ tools/RuinaRPG.Smoke/
# Embedded in RuinaRPG.Infrastructure (see its .csproj).
COPY ["Docs/Sistema RPG/", "Docs/Sistema RPG/"]
# One RUN: MSBuild compiles each shared project once and reuses it for the next publish.
RUN dotnet publish src/RuinaRPG.Api/RuinaRPG.Api.csproj -c Release --no-restore -o /out/api -p:SourceRevisionId=$GIT_SHA \
 && dotnet publish src/RuinaRPG.Client/RuinaRPG.Client.csproj -c Release --no-restore -o /out/client -p:SourceRevisionId=$GIT_SHA \
 && dotnet publish tools/RuinaRPG.Smoke/RuinaRPG.Smoke.csproj -c Release --no-restore -o /out/smoke -p:SourceRevisionId=$GIT_SHA

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app
COPY --from=build /out/api .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RuinaRPG.Api.dll"]

# Holds the published client; deploy/deploy.sh copies /app/wwwroot into webroot/releases/<sha>.
FROM alpine:3.20 AS client
COPY --from=build /out/client/wwwroot /app/wwwroot

# aspnet, not runtime: the smoke uses RuinaRPG.Infrastructure (Identity), which needs Microsoft.AspNetCore.App.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS smoke
WORKDIR /app
COPY --from=build /out/smoke .
ENTRYPOINT ["dotnet", "RuinaRPG.Smoke.dll"]

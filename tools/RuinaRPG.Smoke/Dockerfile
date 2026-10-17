FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish tools/RuinaRPG.Smoke/RuinaRPG.Smoke.csproj -c Release -o /app

# aspnet, not runtime: the smoke uses RuinaRPG.Infrastructure (Identity), which needs Microsoft.AspNetCore.App.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "RuinaRPG.Smoke.dll"]

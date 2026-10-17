FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish tools/RuinaRPG.Smoke/RuinaRPG.Smoke.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "RuinaRPG.Smoke.dll"]

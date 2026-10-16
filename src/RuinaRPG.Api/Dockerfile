FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG GIT_SHA=dev
WORKDIR /src
COPY . .
RUN dotnet publish src/RuinaRPG.Api/RuinaRPG.Api.csproj -c Release -o /app -p:SourceRevisionId=$GIT_SHA

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RuinaRPG.Api.dll"]

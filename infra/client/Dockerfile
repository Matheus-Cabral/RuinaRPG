FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG GIT_SHA=dev
WORKDIR /src
COPY . .
RUN dotnet publish src/RuinaRPG.Client/RuinaRPG.Client.csproj -c Release -o /app -p:SourceRevisionId=$GIT_SHA

# Holds the published client; deploy/deploy.sh copies /app/wwwroot into webroot/releases/<sha>.
FROM alpine:3.20
COPY --from=build /app/wwwroot /app/wwwroot

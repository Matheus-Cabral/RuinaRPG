FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/RuinaRPG.Client/RuinaRPG.Client.csproj -c Release -o /app

FROM nginx:1.27-alpine
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY infra/nginx/nginx.conf /etc/nginx/conf.d/default.conf

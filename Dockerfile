# --- Angular frontend build ---
FROM node:20-alpine AS frontend
WORKDIR /app/src/Giretra.Web/ClientApp/giretra-web
COPY src/Giretra.Web/ClientApp/giretra-web/package.json src/Giretra.Web/ClientApp/giretra-web/package-lock.json ./
RUN npm ci
COPY src/Giretra.Web/ClientApp/giretra-web/ .
RUN npm run build

# --- .NET backend build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY src/Giretra.Core/Giretra.Core.csproj src/Giretra.Core/
COPY src/Giretra.Contracts/Giretra.Contracts.csproj src/Giretra.Contracts/
COPY src/Giretra.Model/Giretra.Model.csproj src/Giretra.Model/
COPY src/Giretra.Web/Giretra.Web.csproj src/Giretra.Web/
RUN dotnet restore src/Giretra.Web/Giretra.Web.csproj
COPY src/Giretra.Core/ src/Giretra.Core/
COPY src/Giretra.Contracts/ src/Giretra.Contracts/
COPY src/Giretra.Model/ src/Giretra.Model/
COPY src/Giretra.Web/ src/Giretra.Web/

FROM build AS publish
RUN dotnet publish src/Giretra.Web/Giretra.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# --- Final runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
COPY --from=frontend /app/src/Giretra.Web/ClientApp/giretra-web/dist/giretra-web/browser wwwroot/
ENTRYPOINT ["dotnet", "Giretra.Web.dll"]

# --- Render worker: same publish output + Chromium (via Playwright) + ffmpeg ---
# aspnet:10.0 is Ubuntu 24.04, whose apt "chromium" is a snap stub, so the browser comes from
# Playwright's own installer (bundled node + cli in the publish output).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS render
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
WORKDIR /app
COPY --from=publish /app/publish .
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg fonts-noto-color-emoji ca-certificates \
    && ./.playwright/node/linux-x64/node ./.playwright/package/cli.js install --with-deps chromium \
    && rm -rf /var/lib/apt/lists/* \
    && chmod -R a+rX /ms-playwright
ENV Render__Enabled=true
ENTRYPOINT ["dotnet", "Giretra.Web.dll", "--render-worker"]

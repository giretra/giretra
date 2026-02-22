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
COPY src/Giretra.Model/Giretra.Model.csproj src/Giretra.Model/
COPY src/Giretra.Web/Giretra.Web.csproj src/Giretra.Web/
RUN dotnet restore src/Giretra.Web/Giretra.Web.csproj
COPY src/Giretra.Core/ src/Giretra.Core/
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

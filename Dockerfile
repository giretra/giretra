# --- Angular frontend build ---
FROM node:20-alpine AS frontend
WORKDIR /src/Giretra.Web/ClientApp/giretra-web
COPY Giretra.Web/ClientApp/giretra-web/package.json Giretra.Web/ClientApp/giretra-web/package-lock.json ./
RUN npm ci
COPY Giretra.Web/ClientApp/giretra-web/ .
RUN npm run build

# --- .NET backend build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Giretra.Core/Giretra.Core.csproj Giretra.Core/
COPY Giretra.Model/Giretra.Model.csproj Giretra.Model/
COPY Giretra.Web/Giretra.Web.csproj Giretra.Web/
RUN dotnet restore Giretra.Web/Giretra.Web.csproj
COPY Giretra.Core/ Giretra.Core/
COPY Giretra.Model/ Giretra.Model/
COPY Giretra.Web/ Giretra.Web/

FROM build AS publish
RUN dotnet publish Giretra.Web/Giretra.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# --- Final runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
COPY --from=frontend /src/Giretra.Web/ClientApp/giretra-web/dist/giretra-web/browser wwwroot/
ENTRYPOINT ["dotnet", "Giretra.Web.dll"]

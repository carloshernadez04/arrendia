# syntax=docker/dockerfile:1

# ---- Compilación ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los archivos de proyecto, para aprovechar la caché de restore
COPY global.json Directory.Build.props .editorconfig ./
COPY src/Arrendia.Core/Arrendia.Core.csproj src/Arrendia.Core/
COPY src/Arrendia.Web/Arrendia.Web.csproj src/Arrendia.Web/
RUN dotnet restore src/Arrendia.Web/Arrendia.Web.csproj

COPY src/ src/
RUN dotnet publish src/Arrendia.Web/Arrendia.Web.csproj -c Release -o /app/publish --no-restore -p:UseAppHost=false

# ---- Ejecución: imagen mínima, sin SDK y con usuario sin privilegios ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=build /app/publish .
# 1654 = usuario "app" sin privilegios de las imágenes oficiales de .NET
USER 1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "Arrendia.Web.dll"]

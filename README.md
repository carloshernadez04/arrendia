# Arrendia

Sistema de administración de arrendamientos para plazas comerciales y edificios.
Proyecto de Programación II (16S – G2), Universidad Doctor Andrés Bello, Regional Sonsonate. Grupo 1.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026, VS Code con C# Dev Kit o Rider
- SQL Server (la estructura de la base de datos **no** vive en este repositorio)

## Estructura

```
src/Arrendia.Web     Aplicación ASP.NET Core MVC
src/Arrendia.Core    Lógica y servicios
tests/Arrendia.Tests Pruebas
seed/seed.sql        Datos iniciales (se ejecutan manualmente)
deploy/              Dockerfile y archivos de despliegue
.github/             Pipelines de CI/CD y Dependabot
```

## Comandos

```bash
dotnet build Arrendia.sln
dotnet test Arrendia.sln
dotnet format Arrendia.sln          # corrige el formato antes de hacer push
dotnet run --project src/Arrendia.Web
```

## Ejecutar en local con Docker

Requiere [Docker Desktop](https://www.docker.com/products/docker-desktop/) abierto.

1. Copia `.env.example` como `.env` y cambia la contraseña de SQL Server.
2. Levanta todo:
   ```bash
   docker compose up -d --build
   ```
3. Abre la aplicación en <http://localhost:5080>. SQL Server queda en `localhost,1433` (usuario `sa`) para conectarte con SSMS.

**Los datos no se pierden:** viven en el volumen `arrendia-sqldata` y se conservan al reiniciar la computadora, al hacer `docker compose down` o al reconstruir la web. Solo se borran con `docker compose down -v`, que **no** deben usar salvo que quieran empezar de cero.

### Estructura de la base de datos

La estructura (tablas, procedimientos y vistas) no vive en este repositorio. Para tenerla en local:

1. Pide el respaldo más reciente (`.bak`) al responsable de la base y cópialo en la carpeta `db-local/`.
2. En SSMS conéctate a `localhost,1433` y restaura el respaldo desde `/var/opt/mssql/backup/`.
3. Si hace falta, carga los datos iniciales:
   ```bash
   docker exec arrendia-db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "TU_CLAVE" -d Arrendia -i /seed/seed.sql
   ```

### Otros comandos

| Comando | Para qué |
|---|---|
| `docker compose up -d db` | Levantar solo SQL Server y correr la web desde Visual Studio |
| `docker compose up -d --build web` | Reconstruir la web después de cambiar código |
| `docker compose logs -f web` | Ver los logs de la aplicación |
| `docker compose down` | Detener todo (los datos se conservan) |

## Ramas

| Rama          | Cómo se actualiza                                   |
|---------------|-----------------------------------------------------|
| `development` | Push directo de cualquier integrante                |
| `qa`          | Solo por Pull Request desde `development` (dueño)   |
| `main`        | Solo por Pull Request desde `qa` (dueño)            |

Antes de cada push: `git pull`, `dotnet build`, `dotnet test` y `dotnet format`.
Nunca subir contraseñas, llaves ni cadenas de conexión: el repositorio es público.

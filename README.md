# Arrendia

Sistema de administración de arrendamientos para plazas comerciales y edificios.
Proyecto de Programación II (16S – G2), Universidad Doctor Andrés Bello, Regional Sonsonate. Grupo 1.


Lo que está hecho y lo que falta: [`TAREAS.md`](TAREAS.md).

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026, VS Code con C# Dev Kit o Rider
- SQL Server (la estructura de la base de datos **no** vive en este repositorio)

## Estructura

```
src/Arrendia.Web     Aplicación ASP.NET Core MVC (Controllers, Models, Views, Services, Data)
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

## Ejecutar en local

Requiere [Docker Desktop](https://www.docker.com/products/docker-desktop/) abierto. Todo corre en tu computadora: no se toca la base del servidor.

1. Copia `.env.example` como `.env` y escribe una contraseña para SQL Server.
2. Pide el respaldo completo más reciente (`Arrendia_completa_v*.bak`, con tablas, procedimientos y seed) y cópialo en `db-local/`.
3. Levanta solo SQL Server:
   ```bash
   docker compose up -d db
   ```
4. Restaura el respaldo como base `Arrendia` (en SSMS conéctate a `localhost,14333` con `sa` y restaura desde `/var/opt/mssql/backup/`, o por consola):
   ```bash
   docker exec arrendia-db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "TU_CLAVE" -Q "RESTORE DATABASE Arrendia FROM DISK='/var/opt/mssql/backup/ARCHIVO.bak' WITH MOVE 'ArrendiaPrueba' TO '/var/opt/mssql/data/Arrendia.mdf', MOVE 'ArrendiaPrueba_log' TO '/var/opt/mssql/data/Arrendia_log.ldf', REPLACE"
   ```
   En Git Bash antepón `MSYS_NO_PATHCONV=1` para que no cambie las rutas.
5. Guarda la cadena de conexión en tus secretos de usuario (una sola vez, no se sube al repositorio):
   ```bash
   dotnet user-secrets set "ConnectionStrings:Arrendia" "Server=localhost,14333;Database=Arrendia;User Id=sa;Password=TU_CLAVE;TrustServerCertificate=True" --project src/Arrendia.Web
   ```
6. Corre la web con F5 en Visual Studio o con `dotnet run --project src/Arrendia.Web` y abre <http://localhost:5247>.

Para volver a empezar con la base limpia, repite el paso 4 (`REPLACE` la sobrescribe).

**Los datos no se pierden:** viven en el volumen `arrendia-sqldata` y se conservan al reiniciar la computadora o al hacer `docker compose down`. Solo se borran con `docker compose down -v`.

También puedes levantar la web dentro de Docker con `docker compose up -d --build` (queda en <http://localhost:5080>).

### Primer administrador

El seed crea al administrador sin contraseña. Para activarlo, genera su enlace (vence en 7 días y se usa una sola vez):

```bash
dotnet run --project src/Arrendia.Web -- activar-admin                         # en tu computadora
docker exec arrendia-web-qa dotnet Arrendia.Web.dll activar-admin               # en el servidor
```

Abre el enlace (antepón el dominio si sale solo la ruta), elige la contraseña y entras directo.

### Correos en desarrollo

Sin servidor SMTP configurado, en `Development` los correos (por ejemplo, el de recuperar contraseña) se escriben en la consola de la aplicación con su enlace. Para enviar correos reales, configura `Smtp:Host`, `Smtp:Puerto`, `Smtp:Usuario`, `Smtp:Contrasena` y `Smtp:Remitente` con `dotnet user-secrets` (nunca en `appsettings.json`).

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

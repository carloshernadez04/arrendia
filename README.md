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

## Ramas

| Rama          | Cómo se actualiza                                   |
|---------------|-----------------------------------------------------|
| `development` | Push directo de cualquier integrante                |
| `qa`          | Solo por Pull Request desde `development` (dueño)   |
| `main`        | Solo por Pull Request desde `qa` (dueño)            |

Antes de cada push: `git pull`, `dotnet build`, `dotnet test` y `dotnet format`.
Nunca subir contraseñas, llaves ni cadenas de conexión: el repositorio es público.

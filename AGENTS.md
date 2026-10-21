# AGENTS.md: guía para asistentes de IA

Este archivo explica a cualquier asistente de IA (y a cualquier persona nueva) qué es Arrendia, cómo está construido y qué reglas no se pueden romper. Léelo completo antes de proponer o escribir código.

El estado del proyecto y lo que falta está en [`TAREAS.md`](TAREAS.md): revísalo antes de empezar y marca lo que termines.

## Qué es Arrendia

Sistema web para administrar arrendamientos de plazas comerciales y edificios de apartamentos en El Salvador. Proyecto estudiantil de Programación II (Universidad Doctor Andrés Bello, Regional Sonsonate, Grupo 1).

Usuarios:

| Rol | Qué hace |
|---|---|
| Administrador | Maneja todo el sistema y la configuración |
| Supervisor | Maneja solo los edificios asignados, con los permisos que le dé el administrador |
| Inquilino | Ve su estado de cuenta, paga en línea, descarga documentos y reporta problemas |
| Técnico | **No tiene cuenta.** Entra con un enlace temporal a un solo reporte de mantenimiento |

### Alcance de esta versión

- **Login:** inicio de sesión, activación por invitación (no hay registro abierto), recuperar contraseña.
- **Menú:** según el rol; tablero con indicadores; portal del inquilino; modo claro y oscuro; adaptable al celular con menú hamburguesa.
- **Mantenimientos:** propiedades, unidades, inquilinos y contactos, contratos (condiciones de cobro y monto del depósito), condiciones por defecto, conceptos de cobro, técnicos (nombre, correo, teléfono, especialidades), usuarios y supervisores, catálogos y parámetros.
- **Consultas:** estado de cuenta, historial de pagos, cobranza por días de atraso, reportes de mantenimiento, documentos, contratos por vencer, bitácora, correos enviados, verificación de documentos por QR, informes (ingresos, morosidad, ocupación, trabajos por técnico) exportables a PDF y Excel.
- **Procesos:** generación automática de cobros, cálculo diario de mora, envío por correo del aviso de cobro y del comprobante, pago en línea con tarjeta, pagos en efectivo o transferencia, cobros extra y condonaciones, firma y activación del contrato, cambio de condiciones, reportes de mantenimiento y enlace para el técnico con PIN.

### Fuera de alcance (no implementar sin que el equipo lo pida)

Salida del inquilino, actas de entrada o salida, liquidación y devolución del depósito, saldo a favor, recordatorios y avisos de mora por correo, recargos por incumplimiento, convenios de pago, disputas, aprobaciones de supervisor, plantillas de contrato editables, cobro de servicios por lectura de medidor, facturación electrónica (DTE), firma electrónica y salarios de técnicos.

## Tecnologías

| Capa | Tecnología |
|---|---|
| Lenguaje y framework | C# con .NET 10 y ASP.NET Core **MVC** (vistas Razor) |
| Base de datos | SQL Server 2022, esquema `dbo` |
| Acceso a datos | Llamadas a procedimientos almacenados y vistas (Dapper recomendado). **No usar Entity Framework migrations** |
| Archivos | Cloudinary con entrega privada y URLs firmadas |
| Pagos | Pasarela de pago en línea (Wompi El Salvador, modo de prueba) |
| PDF y QR | QuestPDF y QRCoder |
| Correo | SMTP de un proveedor transaccional |
| Pruebas | xUnit y `WebApplicationFactory` |
| Contenedores | Docker y Docker Compose |
| CI/CD | GitHub Actions, CodeQL, Gitleaks, Hadolint, Trivy, Dependabot |

## Estructura del repositorio

```
src/Arrendia.Web       Proyecto único ASP.NET Core MVC:
  Controllers/         Reciben la petición y eligen la vista; deben quedar delgados
  Models/              Datos que viajan a las vistas
  Views/               Pantallas Razor
  Services/            Reglas simples y servicios (PDF, correo, pagos en línea, Cloudinary)
  Data/                Código C# que llama procedimientos almacenados y vistas por su nombre
tests/Arrendia.Tests   Pruebas
seed/seed.sql          Datos iniciales; se ejecuta manualmente
deploy/                Dockerfile y required-db-objects.txt
db-local/              Respaldos .bak locales (ignorado por Git)
compose.yaml           SQL Server + web en local, con volumen persistente (SQL en localhost,14333)
.github/               Workflows de CI/CD y Dependabot
```

## Reglas de base de datos (obligatorias)

1. **Ningún SQL de estructura ni de lógica vive en este repositorio.** Las tablas, vistas, funciones, triggers y procedimientos almacenados existen únicamente dentro de SQL Server. Nunca crees archivos `.sql` (salvo `seed/seed.sql`, que solo tiene datos iniciales), migraciones, scripts `CREATE`/`ALTER`, ni pegues el cuerpo de un procedimiento dentro de C# o de la documentación. En `Data/` solo va código C# que llama a un procedimiento o vista **por su nombre** y le pasa parámetros.
2. **La lógica pesada va en SQL y la simple en C#:**
    - **Procedimientos almacenados:** generación de cobros, cálculo de mora, registro y aplicación de pagos, activación y cambio de condiciones de contratos, informes y consultas grandes, y todo lo que modifique varias tablas en una sola operación o deba ser atómico. Para estas operaciones la aplicación llama al procedimiento; no las rearma con varias consultas sueltas.
    - **C# (`Services/` y `Data/` de `Arrendia.Web`):** validaciones de formularios y reglas simples (por ejemplo, que el monto sea mayor a 0 y no supere la deuda antes de enviarlo), formatos, cálculos de presentación y mantenimientos sencillos de catálogos (técnicos, conceptos, feriados) con consultas parametrizadas.
    - Si una regla simple también protege dinero, **se valida en los dos lados**: en C# para dar un mensaje rápido al usuario y en el procedimiento como garantía final.
3. Todo objeto se escribe con su esquema y en **MAYÚSCULAS con guion bajo**: `dbo.CARGO`, `EXEC dbo.SP_REGISTRAR_PAGO`.
    - Tablas en singular (`CONDICION_CONTRATO`); llave primaria `ID`; llaves foráneas `<TABLA>_ID` (`CONTRATO_ID`).
    - Procedimientos `SP_`, vistas `VW_`, funciones `FN_`, triggers `TR_<TABLA>_<ACCION>`, secuencias `SEQ_`.
    - Restricciones `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`; índices `IX_` y únicos `UX_`.
    - En C# las clases y propiedades siguen en PascalCase (`ContratoId`). Dapper las une con `DefaultTypeMap.MatchNamesWithUnderscores = true`.
    - Los scripts que se ejecuten con `sqlcmd` llevan `-I` (o `SET QUOTED_IDENTIFIER ON`): hay índices filtrados y columnas calculadas.
4. Si el código empieza a usar un procedimiento, vista o tabla nueva, **agrégalo a `deploy/required-db-objects.txt`**. El despliegue verifica que exista antes de publicar.
5. Consultas siempre **parametrizadas**. Prohibido concatenar texto del usuario en SQL.
6. Dinero en `decimal` en C# y `DECIMAL(12,2)` en SQL. **Nunca `float` ni `double`** para montos.
7. Las fechas de negocio (vencimientos, días de mora) se calculan en **hora de El Salvador (UTC−6)**; las marcas de tiempo se guardan en UTC.
8. Nada financiero se borra: se **anula** con un ajuste y queda en la bitácora.

## Reglas de negocio que no se pueden romper

- **Pagos:** el monto debe ser **mayor a 0 y menor o igual a lo que se debe**. Si se deben $500, se puede pagar hasta $500, nunca $600. **No existe saldo a favor.**
- Un pago parcial deja el cobro "pagado a medias" y el resto sigue pendiente.
- Un pago en línea **solo se confirma con el aviso (webhook) de la pasarela**, nunca con la redirección del navegador. Se valida la firma del aviso y que el monto coincida.
- El mismo aviso recibido dos veces se registra **una sola vez** (idempotencia por ID de transacción).
- Pagos en efectivo o transferencia: el administrador o un supervisor con permiso marca el cobro como pagado. El **comprobante indica el método**: tarjeta, efectivo o transferencia.
- La mora se calcula por día sobre el saldo vencido, después de los días de gracia, sin pasar el tope y solo si el contrato la tiene activa.
- Cambiar las condiciones de un contrato crea una **versión nueva**; nunca se modifican las condiciones pasadas.
- Una unidad no puede tener dos contratos activos al mismo tiempo.
- **Permisos:** un supervisor solo ve sus edificios y un inquilino solo su información. Verifica la autorización en cada acción del servidor, no solo ocultando botones.
- **Técnico:** el enlace es temporal, revocable y está ligado a un solo reporte. Antes del PIN solo ve la dirección, la descripción y las fotos. Con el PIN que da el inquilino (o el supervisor en áreas comunes) se desbloquea subir fotos del trabajo terminado y cerrarlo. Al quinto PIN incorrecto el enlace se bloquea. El administrador **no ve** el PIN, y el técnico nunca ve montos ni registra costos.

## Seguridad

- **El repositorio es público.** Nunca escribas contraseñas, llaves, tokens ni cadenas de conexión con credenciales en código, `appsettings*.json` ni ejemplos. Se leen de variables de entorno o del archivo `.env`, que está ignorado por Git.
- GitHub bloquea los pushes con secretos y el CI corre Gitleaks. Si aparece un falso positivo, no lo ocultes sin explicar por qué en `.gitleaksignore`.
- Los archivos de Cloudinary nunca son públicos: se entregan con URL firmada de pocos minutos, después de validar el permiso.
- Usa la protección antiforgery de ASP.NET Core en todos los formularios.
- Valida tipo y tamaño de los archivos subidos (solo imágenes y PDF).

## Estilo de código

- C# con `Nullable` activado y **advertencias tratadas como errores**: el código debe compilar sin una sola advertencia.
- Los nombres del dominio van en español (`Cargo`, `Pago`, `Inquilino`, `ReglasPago`); los del framework se quedan como son.
- Namespaces con alcance de archivo (`namespace Arrendia.Web.Services;`) y llaves siempre, aunque el `if` sea de una línea.
- No uses guion bajo en nombres de métodos públicos, incluidos los de las pruebas.
- Toda regla de pagos o cobros nueva lleva **pruebas unitarias** que cubran los casos límite.
- Antes de cada push: `dotnet format Arrendia.sln`, `dotnet build Arrendia.sln -c Release` y `dotnet test Arrendia.sln`.

## Interfaz

Diseño de baja fidelidad (wireframes de todas las pantallas, en escritorio, móvil y modo oscuro): <https://claude.ai/artifact/FyZ6ai2nBXK6bSBqDzXEXc>

- Tipografía: **IBM Plex Sans** para la interfaz e **IBM Plex Mono** para números de documento, códigos y montos en tablas.
- Los colores se definen como **variables CSS** en `:root` y se redefinen para el modo oscuro. Ningún componente usa colores fijos.
- Todo control táctil mide al menos **44 px** de alto.
- En pantallas menores a 640 px: una columna, menú hamburguesa, tablas convertidas en tarjetas y ventanas como hojas desde abajo.
- Contraste mínimo de 4.5:1 en texto, en ambos modos.

| Variable | Modo claro | Modo oscuro | Uso |
|---|---|---|---|
| `--fondo` | `#F4F4F2` | `#141413` | Fondo de la página |
| `--superficie` | `#FFFFFF` | `#1D1D1C` | Tarjetas, tablas, formularios |
| `--lateral` | `#ECECE8` | `#191918` | Menú lateral |
| `--borde` | `#D4D4CF` | `#3A3A37` | Bordes y separadores |
| `--texto` | `#1F1F1E` | `#ECECE8` | Texto principal |
| `--texto-secundario` | `#5E5E5A` | `#A3A39D` | Etiquetas y texto de apoyo |
| `--relleno` | `#E7E7E3` | `#2A2A28` | Imágenes pendientes, áreas vacías |
| `--acento` | `#2F5BD3` | `#8FAEF7` | Enlaces, foco, pestaña activa |
| `--acento-boton` | `#2F5BD3` | `#3B62D6` | Botón principal (texto blanco) |
| `--acento-suave` | `#EAF0FD` | `#1E2A47` | Notas, selección |
| `--correcto` / fondo | `#1E5631` / `#E1EEE4` | `#8FD3A2` / `#183424` | Al día, pagado |
| `--atencion` / fondo | `#6E4400` / `#FBEFD6` | `#F0C46E` / `#3A2C10` | Parcial, por vencer |
| `--problema` / fondo | `#8A2B2B` / `#F7E0E0` | `#F2A3A3` / `#3E1A1A` | En mora, anulado, error |

## Git y ramas

| Rama | Cómo se actualiza |
|---|---|
| `development` | Push directo de cualquier integrante. El CI corre después de cada push |
| `qa` | Solo por Pull Request desde `development`, con el CI en verde; solo lo fusiona el dueño |
| `main` | Solo por Pull Request desde `qa`, con el CI en verde; solo lo fusiona el dueño |

- Siempre `git pull` antes de empezar y antes de hacer push.
- **Nunca `git push --force`** ni reescribir historial.
- Mensajes de commit en español, cortos y en imperativo: "Agrega mantenimiento de técnicos".
- **No agregues firmas ni atribuciones de IA** en commits ni PRs (nada de `Co-Authored-By` de asistentes ni "Generated with…").

## Comandos

```bash
dotnet build Arrendia.sln
dotnet test Arrendia.sln
dotnet format Arrendia.sln
dotnet run --project src/Arrendia.Web
docker compose up -d --build     # SQL Server + web en local (datos persistentes)
docker compose up -d db          # solo SQL Server
docker compose down              # detiene sin borrar datos; NUNCA usar -v sin querer borrar la base
```

## Lo que nunca debes hacer

- Guardar SQL en el repositorio: estructura, procedimientos, vistas, migraciones o scripts (lo único permitido es `seed/seed.sql`).
- Usar `float` o `double` para dinero.
- Permitir pagos mayores a la deuda o crear saldo a favor.
- Confirmar un pago sin el webhook validado.
- Subir secretos, archivos `.env` o respaldos `.bak`.
- Desactivar analizadores, pruebas o pasos del CI para que algo pase.
- Implementar funciones del apartado "Fuera de alcance" sin que el equipo lo decida.
- Usar `docker compose down -v`, que borra la base de datos local.

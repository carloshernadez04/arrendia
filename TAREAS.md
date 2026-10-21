# Tareas de Arrendia

Estado del proyecto. Marca `[x]` cuando una tarea quede terminada y desplegada en QA, y agrega las nuevas donde correspondan.

## Planeación y diseño

- [x] Alcance funcional y anteproyecto
- [x] Diseño técnico y diseño de la base de datos
- [x] Wireframes de todas las pantallas (escritorio, móvil y modo oscuro)
- [x] Prototipo interactivo como guía visual
- [x] Ubicación exacta de propiedades en Maps (coordenadas) e invitaciones definidas en el diseño
- [ ] Actualizar la guía de base de datos con los nombres en mayúsculas
- [ ] Actualizar el diseño técnico y los wireframes con los últimos cambios del alcance

## Repositorio y equipo

- [x] Repositorio con ramas `development`, `qa` y `main`, reglas de protección y CODEOWNERS
- [x] `AGENTS.md` con las reglas del proyecto y `README.md` con los comandos
- [x] Agregar a Marvin y Cristian como colaboradores
- [ ] Agregar a Melvin como colaborador
- [ ] Decidir si se reescribe el primer commit (quitar la línea de coautoría; requiere force push)

## Pipeline y servidor

- [x] CI: compilar, pruebas con cobertura, búsqueda de secretos, imagen Docker y CodeQL
- [x] Despliegue a QA: Trivy, despliegue con regreso automático, pruebas de humo y OWASP ZAP
- [x] Despliegue a producción: revisión previa, aprobación del dueño, respaldo, pruebas de humo y release
- [x] Revisión semanal de dependencias e imágenes desplegadas
- [x] SQL Server en el servidor con bases `ArrendiaQA` y `ArrendiaProd` y usuarios separados
- [x] Respaldo diario automático y prueba semanal de restauración
- [x] Dominios de QA y producción por Cloudflare Tunnel
- [ ] Corregir el paso que bloquea llaves de producción de Wompi en QA (formato de El Salvador)
- [ ] Cargar las llaves de SMTP, Wompi y Cloudinary en el `DOTENV` de cada ambiente
- [ ] Verificar el dominio en el proveedor SMTP (registros SPF y DKIM en Cloudflare)
- [ ] Borrar los registros DNS basura en horasvoluntariado.site
- [ ] Migrar a los dominios `arrendia.site` y `qa.arrendia.site`
- [ ] Avisos del pipeline en Slack (opcional)

## Base de datos

- [x] Estructura: 36 tablas, 99 llaves foráneas, secuencias e índices
- [x] Seed con catálogos, conceptos, parámetros, plantillas, feriados y usuarios base
- [x] Lógica: 38 procedimientos, 13 funciones, 10 vistas y 9 triggers, con 72 pruebas
- [x] Aplicada en QA y en producción
- [ ] Script de datos de ejemplo solo para QA (propiedades, inquilinos, contratos)

## Aplicación: base técnica

- [x] Diseño base: paleta, tipografías, menú por rol, modo claro y oscuro, versión móvil
- [x] Acceso a datos con Dapper y `SESSION_CONTEXT`
- [x] Inicio de sesión, bloqueo por intentos, cerrar sesión y límite de envíos por IP
- [x] Activar invitación, recuperar y restablecer contraseña
- [x] Comando `activar-admin` para el primer administrador
- [x] `/health` con revisión de la base y claves de sesión persistentes
- [ ] Servicio en segundo plano que ejecuta `SP_EJECUTAR_PROCESOS_DIARIOS` cada hora
- [ ] Envío de la cola de correos (aviso de cobro y comprobante)
- [ ] Subida de archivos a Cloudinary con URL firmada
- [ ] Generación de PDF con QuestPDF y código QR con QRCoder

## Aplicación: módulos

- [ ] Mi cuenta: nombre, contraseña, correo de acceso con confirmación, tema y cerrar sesión en todos los dispositivos
- [ ] Usuarios y permisos: invitar supervisores, propiedades asignadas, permisos e invitaciones pendientes
- [ ] Configuración: plantilla de condiciones, conceptos de cobro, catálogos, feriados y parámetros
- [ ] Propiedades y unidades, con plano de unidades y ubicación en Maps
- [ ] Inquilinos y contactos, con acceso al portal
- [ ] Contratos: asistente, PDF para firmar, activación con el escaneado y cambio de condiciones
- [ ] Cargos: cargos manuales, condonación y anulación
- [ ] Pagos en efectivo y transferencia, con comprobante en PDF que indique el método
- [ ] Pago en línea con Wompi: intención de pago, enlace y webhook con firma validada
- [ ] Cobranza por días de atraso
- [ ] Reportes de mantenimiento y técnicos: asignación por especialidad y enlace con PIN
- [ ] Portal del inquilino: estado de cuenta, pagar, documentos y reportes
- [ ] Tablero con indicadores
- [ ] Informes exportables a PDF y Excel
- [ ] Documentos y verificación por QR
- [ ] Correos enviados y bitácora

## Calidad antes de producción

- [ ] Pruebas de pagos en el CI (monto mayor a cero, sin pagar de más, pagos parciales)
- [ ] Pago de prueba de punta a punta en QA con la tarjeta de prueba de Wompi
- [ ] Revisión final de seguridad antes de pasar cada módulo a producción

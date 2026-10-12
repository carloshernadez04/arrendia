#!/usr/bin/env bash
# Respaldo completo de una base (antes de desplegar y cada noche). Conserva los últimos 14.
# Express no admite COMPRESSION; CHECKSUM sí.
# Uso: respaldar-bd.sh <NombreBaseDatos>
set -euo pipefail
BD="$1"
SA=$(grep '^MSSQL_SA_PASSWORD=' "$HOME/apps/arrendia/sql/.env" | cut -d= -f2-)
ARCHIVO="${BD}_$(date +%Y%m%d_%H%M%S).bak"
docker exec -e SQLCMDPASSWORD="$SA" arrendia-sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b \
  -Q "BACKUP DATABASE [$BD] TO DISK = N'/var/opt/mssql/backup/$ARCHIVO' WITH COPY_ONLY, CHECKSUM, INIT"
echo "Respaldo creado: $ARCHIVO"
ls -1t "$HOME/apps/arrendia/respaldos/${BD}_"*.bak 2>/dev/null | tail -n +15 | xargs -r rm -f

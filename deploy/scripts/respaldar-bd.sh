#!/usr/bin/env bash
# Respaldo completo de una base antes de desplegar. Conserva los últimos 7.
# Uso: respaldar-bd.sh <NombreBaseDatos>
set -euo pipefail
BD="$1"
SA=$(grep '^MSSQL_SA_PASSWORD=' "$HOME/apps/arrendia/sql/.env" | cut -d= -f2-)
ARCHIVO="${BD}_$(date +%Y%m%d_%H%M%S).bak"
docker exec arrendia-sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SA" -b \
  -Q "BACKUP DATABASE [$BD] TO DISK = N'/var/opt/mssql/backup/$ARCHIVO' WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT"
echo "Respaldo creado: $ARCHIVO"
ls -1t "$HOME/apps/arrendia/respaldos/${BD}_"*.bak 2>/dev/null | tail -n +8 | xargs -r rm -f

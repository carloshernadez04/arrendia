#!/usr/bin/env bash
# Prueba que el último respaldo de producción se pueda restaurar y que esté íntegro.
# Restaura en una base temporal, ejecuta DBCC CHECKDB y la elimina. No toca ArrendiaProd.
set -euo pipefail
SA=$(grep '^MSSQL_SA_PASSWORD=' "$HOME/apps/arrendia/sql/.env" | cut -d= -f2-)
ULTIMO=$(ls -1t "$HOME/apps/arrendia/respaldos/ArrendiaProd_"*.bak 2>/dev/null | head -1 || true)
if [ -z "$ULTIMO" ]; then echo "::warning::Todavía no hay respaldos de ArrendiaProd."; exit 0; fi
ARCHIVO=$(basename "$ULTIMO")
echo "Probando restauración de $ARCHIVO"
sql() { docker exec -e SQLCMDPASSWORD="$SA" arrendia-sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b -h -1 -W "$@"; }
LOGICOS=$(sql -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'/var/opt/mssql/backup/$ARCHIVO'" -s '|' | awk -F'|' 'NF>2{print $1"|"$3}')
MOVE=""
while IFS='|' read -r nombre tipo; do
  [ -z "$nombre" ] && continue
  ext=$([ "$tipo" = "L" ] && echo ldf || echo mdf)
  MOVE="$MOVE, MOVE N'$nombre' TO N'/var/opt/mssql/data/ArrendiaPruebaRestauracion_$nombre.$ext'"
done <<< "$LOGICOS"
sql -Q "IF DB_ID('ArrendiaPruebaRestauracion') IS NOT NULL DROP DATABASE ArrendiaPruebaRestauracion"
sql -Q "RESTORE DATABASE ArrendiaPruebaRestauracion FROM DISK = N'/var/opt/mssql/backup/$ARCHIVO' WITH REPLACE, CHECKSUM$MOVE"
sql -Q "DBCC CHECKDB (ArrendiaPruebaRestauracion) WITH NO_INFOMSGS"
sql -Q "DROP DATABASE ArrendiaPruebaRestauracion"
echo "El respaldo $ARCHIVO se restauró y está íntegro."

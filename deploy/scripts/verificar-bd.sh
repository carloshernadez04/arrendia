#!/usr/bin/env bash
# Verifica que en la base del ambiente existan los objetos que necesita esta versión.
# Uso: verificar-bd.sh <NombreBaseDatos>
set -euo pipefail
BD="$1"
LISTA="deploy/required-db-objects.txt"
SA=$(grep '^MSSQL_SA_PASSWORD=' "$HOME/apps/arrendia/sql/.env" | cut -d= -f2-)
faltan=0
while IFS= read -r obj; do
  obj="${obj%%#*}"; obj="$(echo "$obj" | xargs)"
  [ -z "$obj" ] && continue
  existe=$(docker exec arrendia-sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SA" -d "$BD" -h -1 -W \
    -Q "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'$obj') IS NULL THEN 0 ELSE 1 END" | tr -d '[:space:]')
  if [ "$existe" != "1" ]; then echo "::error::Falta en $BD: $obj"; faltan=$((faltan+1)); else echo "OK  $obj"; fi
done < "$LISTA"
if [ "$faltan" -gt 0 ]; then echo "::error::Faltan $faltan objetos en $BD. Actualiza la base antes de desplegar."; exit 1; fi
echo "La base $BD tiene todo lo que necesita esta versión."

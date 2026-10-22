#!/usr/bin/env bash
set -euo pipefail
BD="$1"
SEED="seed/seed.sql"
SA=$(grep '^MSSQL_SA_PASSWORD=' "$HOME/apps/arrendia/sql/.env" | cut -d= -f2-)
if grep -qiE '^\s*(UPDATE|DELETE|MERGE|TRUNCATE|DROP|ALTER)\b' "$SEED"; then
  echo "::error::El seed solo puede insertar datos que falten."
  exit 1
fi
DESTINO="/tmp/seed_${BD}_$$.sql"
docker cp "$SEED" "arrendia-sql:$DESTINO" >/dev/null
trap 'docker exec -u 0 arrendia-sql rm -f "$DESTINO"' EXIT
docker exec -e SQLCMDPASSWORD="$SA" arrendia-sql /opt/mssql-tools18/bin/sqlcmd -C -I -S localhost -U sa -d "$BD" -b -i "$DESTINO" >/dev/null
echo "Seed aplicado en $BD."

#!/usr/bin/env bash
# Lo ejecuta cron cada hora en el servidor. Si una base todavía no tiene respaldo de hoy, lo crea.
# Así no se pierde el respaldo del día aunque el servidor haya estado apagado a cierta hora.
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
HOY=$(TZ=America/El_Salvador date +%Y%m%d)
if [ "$(docker inspect -f '{{.State.Health.Status}}' arrendia-sql 2>/dev/null)" != "healthy" ]; then
  echo "$(date '+%F %T') SQL Server todavía no está listo; se intenta en la próxima hora."
  exit 0
fi
for BD in ArrendiaProd ArrendiaQA; do
  if ls "$HOME/apps/arrendia/respaldos/${BD}_${HOY}_"*.bak >/dev/null 2>&1; then continue; fi
  echo "$(date '+%F %T') Respaldando $BD"
  "$DIR/respaldar-bd.sh" "$BD" | tail -1
done

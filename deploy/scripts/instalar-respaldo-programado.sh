#!/usr/bin/env bash
# Copia los scripts de respaldo a ~/apps/arrendia/scripts y registra la tarea de cron (idempotente).
set -euo pipefail
DESTINO="$HOME/apps/arrendia/scripts"
mkdir -p "$DESTINO" "$HOME/apps/arrendia/logs"
install -m 755 deploy/scripts/respaldar-bd.sh deploy/scripts/respaldo-del-dia.sh "$DESTINO/"
LINEA="0 * * * * $DESTINO/respaldo-del-dia.sh >> $HOME/apps/arrendia/logs/respaldo.log 2>&1"
{ crontab -l 2>/dev/null | grep -v 'respaldo-del-dia.sh' || true; echo "$LINEA"; } | crontab -
echo "Tarea programada:"; crontab -l | grep 'respaldo-del-dia.sh'

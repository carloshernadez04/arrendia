#!/usr/bin/env bash
# Pruebas de humo contra la dirección pública (pasa por Cloudflare, igual que un usuario real).
# Uso: humo.sh <url-base>
set -euo pipefail
URL="${1%/}"
falla=0
revisar() {
  local nombre="$1" ruta="$2" esperado="$3"
  local codigo tiempo
  read -r codigo tiempo < <(curl -sS -o /tmp/humo.body -w "%{http_code} %{time_total}" --max-time 20 "$URL$ruta" || echo "000 0")
  if [ "$codigo" = "$esperado" ]; then echo "OK   $nombre ($ruta) -> $codigo en ${tiempo}s"
  else echo "::error::$nombre ($ruta) devolvió $codigo, se esperaba $esperado"; falla=1; fi
}
revisar "Página inicial" "/" 200
revisar "Salud" "/health" 200
grep -q "Healthy" /tmp/humo.body || { echo "::error::/health no respondió Healthy"; falla=1; }
revisar "Ruta inexistente" "/no-existe-$RANDOM" 404
for h in x-content-type-options x-frame-options content-security-policy strict-transport-security; do
  if curl -sSI --max-time 20 "$URL/" | grep -qi "^$h:"; then echo "OK   encabezado $h"
  else echo "::error::Falta el encabezado $h"; falla=1; fi
done
exit $falla

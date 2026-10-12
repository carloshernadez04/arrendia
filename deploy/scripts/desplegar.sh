#!/usr/bin/env bash
# Despliega una imagen en el servidor con verificación de salud y regreso automático.
# Uso: desplegar.sh <qa|prod> <imagen> <puerto>    (requiere la variable DOTENV con el contenido del .env)
set -euo pipefail
AMBIENTE="$1"; IMAGEN="$2"; APP_PORT="$3"
DIR="$HOME/apps/arrendia/$AMBIENTE"
mkdir -p "$DIR"
cp deploy/compose.server.yaml "$DIR/compose.yaml"
umask 077
printf '%s\n' "$DOTENV" > "$DIR/.env"
cd "$DIR"
export AMBIENTE APP_PORT

ANTERIOR=""
[ -f imagen-actual ] && ANTERIOR="$(cat imagen-actual)"

levantar() { IMAGEN="$1" docker compose up -d --pull always --remove-orphans; }
saludable() {
  for _ in $(seq 1 30); do
    if curl -fsS "http://127.0.0.1:$APP_PORT/health" >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  return 1
}

echo "Desplegando $IMAGEN en $AMBIENTE (puerto $APP_PORT)"
levantar "$IMAGEN"
if saludable && curl -fsS -o /dev/null "http://127.0.0.1:$APP_PORT/"; then
  echo "$IMAGEN" > imagen-actual
  echo "Despliegue correcto en $AMBIENTE."
  exit 0
fi

echo "::error::La nueva versión no respondió en /health."
docker compose logs --tail 80 web || true
if [ -n "$ANTERIOR" ]; then
  echo "Regresando a la versión anterior: $ANTERIOR"
  levantar "$ANTERIOR"
  saludable && echo "Versión anterior restaurada." || echo "::error::Tampoco respondió la versión anterior."
fi
exit 1

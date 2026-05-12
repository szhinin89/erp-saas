#!/bin/bash

# ========================================================
# Script de prueba automática para first-run (Super Admin)
# Requiere: API en ejecución + curl
# Opcional: jq para parseo JSON elegante
# ========================================================

set -euo pipefail

API_URL="${API_URL:-https://localhost:5001}"
RESET_ENDPOINT="/api/dev/reset-first-run"
SETUP_ENDPOINT="/api/setup/claim-initial-superadmin"
LOGIN_ENDPOINT="/api/auth/superadmin-login"

EMAIL="${EMAIL:-superadmin@prueba.com}"
PASSWORD="${PASSWORD:-SuperSegura123!}"
FIRST_NAME="${FIRST_NAME:-Super}"
LAST_NAME="${LAST_NAME:-Admin Test}"

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}🧪 Iniciando prueba automática first-run...${NC}"

echo -e "${YELLOW}1) Verificando API en ${API_URL}${NC}"
if ! curl -k -s -o /dev/null -w "%{http_code}" "${API_URL}/swagger/index.html" | grep -q "200"; then
  echo -e "${RED}❌ API no accesible. Inicia la API primero.${NC}"
  exit 1
fi
echo -e "${GREEN}✅ API accesible.${NC}"

echo -e "${YELLOW}2) Reseteando estado first-run (solo Development)${NC}"
RESET_RESPONSE="$(curl -k -s -X POST "${API_URL}${RESET_ENDPOINT}")"
echo "$RESET_RESPONSE"

if command -v jq >/dev/null 2>&1; then
  TOKEN="$(echo "$RESET_RESPONSE" | jq -r '.responseObject.SetupToken // empty')"
  MSG="$(echo "$RESET_RESPONSE" | jq -r '.responseObject.Message // .message // empty')"
else
  TOKEN="$(echo "$RESET_RESPONSE" | sed -n 's/.*"SetupToken":"\([^"]*\)".*/\1/p')"
  MSG="$(echo "$RESET_RESPONSE" | sed -n 's/.*"Message":"\([^"]*\)".*/\1/p')"
fi

if [ -z "$TOKEN" ]; then
  echo -e "${RED}❌ No se pudo obtener SetupToken desde /api/dev/reset-first-run.${NC}"
  echo -e "${YELLOW}Respuesta: ${RESET_RESPONSE}${NC}"
  exit 1
fi
echo -e "${GREEN}✅ First-run reseteado (${MSG:-ok}).${NC}"
echo -e "${GREEN}✅ Token capturado automáticamente.${NC}"

echo -e "${YELLOW}3) Creando primer SuperAdmin con token efímero${NC}"
CREATE_PAYLOAD=$(cat <<EOF
{
  "setupToken": "$TOKEN",
  "email": "$EMAIL",
  "firstName": "$FIRST_NAME",
  "lastName": "$LAST_NAME",
  "password": "$PASSWORD"
}
EOF
)

CREATE_RESPONSE="$(curl -k -s -X POST "${API_URL}${SETUP_ENDPOINT}" -H "Content-Type: application/json" -d "$CREATE_PAYLOAD")"
echo "$CREATE_RESPONSE"

if ! echo "$CREATE_RESPONSE" | grep -qi '"success":[[:space:]]*true'; then
  echo -e "${RED}❌ Fallo al crear SuperAdmin.${NC}"
  exit 1
fi
echo -e "${GREEN}✅ SuperAdmin creado correctamente.${NC}"

echo -e "${YELLOW}4) Validando login del SuperAdmin${NC}"
LOGIN_PAYLOAD=$(cat <<EOF
{
  "email": "$EMAIL",
  "password": "$PASSWORD"
}
EOF
)

LOGIN_RESPONSE="$(curl -k -s -X POST "${API_URL}${LOGIN_ENDPOINT}" -H "Content-Type: application/json" -d "$LOGIN_PAYLOAD")"
echo "$LOGIN_RESPONSE"

if echo "$LOGIN_RESPONSE" | grep -qi '"token":[[:space:]]*"'; then
  echo -e "${GREEN}✅ Login exitoso. SuperAdmin operativo.${NC}"
else
  echo -e "${RED}❌ Login fallido.${NC}"
  exit 1
fi

echo -e "${GREEN}🎉 Prueba first-run completada con éxito.${NC}"

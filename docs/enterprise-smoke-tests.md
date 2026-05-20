# Enterprise smoke tests

Validación manual (y con `curl`) del flujo SaaS + ERP sin romper auth, billing, límites ni switch de empresa.

**Requisitos**

- API: `http://localhost:5003` (o `https://localhost:5001`)
- Frontend (opcional UI): `http://localhost:5173`
- Seed demo: `Development:SeedDemoTenant: true` en `ERP.API/appsettings.Development.json`
- Credenciales demo: `admin@erp.com` / `Admin123!`
- Suscriptor demo slug: `subscriber-demo`

Variables para scripts:

```bash
API=http://localhost:5003
EMAIL=admin@erp.com
PASS=Admin123!
```

---

## 1. Login → contexto suscriptor

```bash
curl -s -X POST "$API/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\",\"subscriberId\":\"\"}" \
  | jq .
```

**Esperado:** `success: true`, `data.token` presente, `data.user.subscriberId` del demo.

Guardar token:

```bash
TOKEN=$(curl -s -X POST "$API/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}" \
  | jq -r '.data.token')
```

---

## 2. Listar empresas accesibles (`my-companies`)

```bash
curl -s "$API/api/auth/my-companies" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

**Esperado:** al menos una empresa con `companyId`.

```bash
COMPANY_ID=$(curl -s "$API/api/auth/my-companies" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.data[0].companyId')
```

---

## 3. Select-company (`switch-company`)

```bash
curl -s -X POST "$API/api/auth/switch-company" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"companyId\":\"$COMPANY_ID\"}" \
  | jq .
```

**Esperado:** nuevo `data.token` con claim `company_id` (decodificar JWT en [jwt.io](https://jwt.io)).

```bash
TOKEN=$(curl -s -X POST "$API/api/auth/switch-company" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"companyId\":\"$COMPANY_ID\"}" \
  | jq -r '.data.token')
```

---

## 4. Refresh mantiene `company_id`

Obtener refresh (cookie `erp_refresh_token` tras login/switch, o body):

```bash
REFRESH=$(curl -s -c /tmp/erp-cookies.txt -X POST "$API/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}" -o /tmp/login.json \
  && curl -s -b /tmp/erp-cookies.txt -X POST "$API/api/auth/switch-company" \
  -H "Authorization: Bearer $(jq -r '.data.token' /tmp/login.json)" \
  -H "Content-Type: application/json" \
  -d "{\"companyId\":\"$COMPANY_ID\"}" -c /tmp/erp-cookies.txt | jq -r '.data.token')

curl -s -b /tmp/erp-cookies.txt -X POST "$API/api/auth/refresh" \
  -H "Content-Type: application/json" | jq .
```

**Esperado:** `data.token` con el mismo `company_id` que antes del refresh.

---

## 5. Empresa operativa actual

```bash
curl -s "$API/api/companies/current" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

**Esperado:** `data.id` = `$COMPANY_ID`.

---

## 6. Switch-company a otra empresa (si existe)

Si hay segunda empresa en `my-companies`, repetir paso 3 con otro `companyId` y verificar que `/api/companies/current` cambia.

---

## 7. MAX_COMPANIES (starter = 1 empresa)

Con plan `starter`, el suscriptor demo ya tiene 1 empresa activa. Crear otra debe devolver **403**:

```bash
curl -s -o /tmp/create-co.json -w "%{http_code}" -X POST "$API/api/companies" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "taxId":"1791234567001",
    "legalName":"Segunda Empresa SA",
    "mainAddress":"Quito"
  }'
echo
cat /tmp/create-co.json | jq .
```

**Esperado:** HTTP `403`, mensaje de límite comercial (`MAX_COMPANIES`).

---

## 8. Billing read-only

```bash
curl -s "$API/api/saas/billing/account" -H "Authorization: Bearer $TOKEN" | jq .
curl -s "$API/api/saas/billing/invoices?take=5" -H "Authorization: Bearer $TOKEN" | jq .
curl -s "$API/api/saas/billing/events?take=5" -H "Authorization: Bearer $TOKEN" | jq .
```

**Esperado:** HTTP 200 (puede devolver listas vacías en demo).

---

## 9. Forbidden sin membership / empresa ajena

1. Login con usuario sin membership en empresa B, o
2. `switch-company` con `companyId` de otra organización:

```bash
curl -s -w "\n%{http_code}\n" -X POST "$API/api/auth/switch-company" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"companyId":"00000000-0000-0000-0000-000000000099"}'
```

**Esperado:** 400/403 según validación de membership.

Endpoint ERP sin `company_id` en JWT (solo login sin switch):

```bash
LOGIN_ONLY=$(curl -s -X POST "$API/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}" | jq -r '.data.token')

curl -s -w "\n%{http_code}\n" "$API/api/inventory/warehouses" \
  -H "Authorization: Bearer $LOGIN_ONLY"
```

**Esperado:** 403 por `CompanyScopeBehavior` (sin contexto operativo).

---

## 10. Ventas oleada 2 (con company en JWT)

Tras `switch-company` (sección 3), listar facturas:

```bash
curl -s "$API/api/sales/invoices?pageSize=5" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

Crear borrador (ajustar IDs de cliente, bodega, sucursal y producto del tenant demo):

```bash
curl -s -X POST "$API/api/sales/invoices" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "<CLIENTE_UUID>",
    "warehouseId": "<BODEGA_UUID>",
    "branchId": "<SUCURSAL_UUID>",
    "items": [{ "productId": "<PRODUCTO_UUID>", "quantity": 1, "unitPrice": 10 }]
  }' | jq .
```

Validar y emitir (SRI simulado en Development):

```bash
FACTURA_ID=<uuid_devuelto>
curl -s -X PATCH "$API/api/sales/invoices/$FACTURA_ID/validar" \
  -H "Authorization: Bearer $TOKEN" | jq .
curl -s -X PATCH "$API/api/sales/invoices/$FACTURA_ID/emitir" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

Stock disponible (misma empresa):

```bash
curl -s "$API/api/sales/invoices/stock?productoId=<PRODUCTO_UUID>&bodegaId=<BODEGA_UUID>" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

Cambiar a otra empresa (`COMPANY_B`) y repetir `GET` de la factura por id:

```bash
TOKEN_B=$(curl -s -X POST "$API/api/auth/switch-company" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"companyId\":\"$COMPANY_B\"}" | jq -r '.data.token')

curl -s -w "\n%{http_code}\n" "$API/api/sales/invoices/$FACTURA_ID" \
  -H "Authorization: Bearer $TOKEN_B"
```

**Esperado:** 404 o lista sin la factura de la empresa A.

---

## 11. Inventario oleada 1 (con company en JWT)

```bash
curl -s "$API/api/inventory/warehouses" \
  -H "Authorization: Bearer $TOKEN" | jq .
```

**Esperado:** 200 y listado acotado a `company_id` del token.

---

## UI rápida

1. `npm run dev` en `frontend/`
2. Login → si SuperAdmin, `/select-subscriber` → `/select-company` → `/dashboard`
3. Refrescar página: debe permanecer en dashboard con misma empresa
4. Company switcher: cambiar empresa y verificar recarga de contexto

---

## Health

```bash
curl -s "$API/health/live"
```

**Esperado:** HTTP 200.

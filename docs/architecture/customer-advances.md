# Anticipos de clientes — SSOT (Finance)

Fuente vigente del comportamiento **implementado y probado** de Anticipos de Clientes. Hoy: registro y consultas
(CUSTOMER-ADVANCES-V1 CA-F3) sobre el dominio de CA-F1 y la contabilidad de CA-F2. **No existen** operaciones de
aplicación a CxC, devolución, reversa ni anulación, ni pantallas o menú.

Decisiones: [ADR-052](../decisions/ADR-052-customer-advances-v1-architecture.md). Contrato contable:
[Accounting §28](accounting.md#28-anticipos-de-clientes-customer-advances-v1-ca-f2). Idempotencia y locks:
[Backend](backend.md#idempotencia-de-comandos-financieros-creadores-de-dinero-zh-financial-command-idempotency-01).
Entrega: [STATUS](../../STATUS.md).

## 1. Modelo

`CustomerAdvance` (tenant + empresa) con un único libro `CustomerAdvanceMovement`. El anticipo nace `Confirmed`, sin
borrador, con disponible igual al original y un movimiento `Received`. El saldo es el disponible que mantiene ese libro: no
existe otro. No se crea factura, venta, cuenta por cobrar ni `Payment`. El historial no se borra ni se reescribe a través
del contexto; cada operación genera Entity Audit (`customer_advance_audit`).

## 2. API

Base `api/v1/finance/customer-advances`, envelope `ApiResponse<T>`.

| Método y ruta | Permiso | Alcance de contexto |
|---|---|---|
| `POST /` → 201 | `finance.customer-advances.create` | Empresa y sucursal activa autorizada |
| `GET /?page&pageSize&customerId&status&branchId` | `finance.view` | Empresa; `pageSize` 1–100 (25 por defecto) |
| `GET /{id}` (con movimientos) | `finance.view` | Empresa |
| `GET /balance?customerId=` | `finance.view` | Empresa; Σ disponibles de anticipos `Confirmed` |

La sucursal identifica el origen y es un filtro opcional del listado: nunca limita el detalle ni el saldo.

**Permisos.** El catálogo del Kernel declara además `finance.customer-advances.apply`, `.reverse` y `.refund`, sin
operación que los use. Ninguno de los cuatro está relacionado todavía con un ítem de navegación, por lo que **no son
asignables a perfiles**: hoy registra solo quien tiene acceso de administrador.

## 3. Registro

Entrada: `clientRequestId`, `customerId`, `amount`, `advanceDate`, `paymentMethodId`, `companyBankAccountId` **o**
`cashRegisterId`, `reference` (≤ 100), `reason` (≤ 500). Tenant, empresa, sucursal, moneda de la empresa y usuario salen
del contexto.

| Regla | Rechazo |
|---|---|
| Monto > 0 con la precisión monetaria fija; exactamente un destino | 422 |
| Socio de negocio del tenant (compartido por sus empresas), activo y con rol Cliente activo | 404 / 422 |
| Medio activo; efectivo físico → caja; transferencia (`DetailType = Transfer`) → cuenta bancaria; tarjeta, cheque, crédito y otros no se admiten | 404 / 422 |
| Referencia obligatoria si el medio la exige | 422 |
| Destino de la empresa y activo; su cuenta contable, de la empresa | 404 / 422 |
| Efectivo: turno abierto de esa caja y usuario titular; la caja puede ser de otra sucursal de la empresa | 422 |
| Empresa con modo contable decidido y fecha fuera de un período Closed/Locked | 422 |

En efectivo se registra en el turno un `ManualIncome` sin motivo con `ReferenceType = CustomerAdvance` (6) y
`ReferenceId` = movimiento `Received`, vinculado al anticipo; la transferencia no genera movimiento de caja. La referencia
visible en Caja se omite si no cabe (no se trunca).

Anticipo, movimiento, caja, auditoría, obligación contable y outbox se confirman en una transacción o no queda nada. El
asiento se intenta después del commit: una cuenta o regla inválida deja la obligación `Failed` reprocesable y el anticipo
vigente. El movimiento de caja nunca es fuente contable.

## 4. Idempotencia

Huella canónica V1 con el tipo de operación y solo los datos del usuario. Replay por tenant + empresa + clave: misma huella
→ 201 con el mismo anticipo, en su estado vigente; otra huella → 409; clave ya usada en otra empresa del tenant → 409 sin
datos de ella. Un rechazo no consume la clave. Barrera definitiva: índices únicos de la clave.

## 5. Evidencia

`ERP.API.Tests/Integration/CustomerAdvanceApiTests` (HTTP + PostgreSQL), `CustomerAdvance*` en Domain, Application e
Infrastructure.

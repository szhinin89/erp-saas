# Política: formularios, acceso, planes y UX (borrador para revisión)

**Estado:** borrador — revisión interna.  
**Alcance:** define reglas para **formularios nuevos** y para **cambios sustanciales** a formularios existentes. No obliga refactor masivo del legado hasta que una pantalla entre en mantenimiento.

---

## 1. RBAC, multi-tenant y roles

1. **Administrador de empresa (tenant)**  
   - Solo opera sobre datos **de su `tenant_id`** (aislamiento estricto en API y consultas).  
   - La **visibilidad** de pantallas y acciones se gobierna con **permisos** (`catalog.*`, `saas.*`, etc.) y, donde aplique, con **features del plan** del tenant (ver §2).

2. **SuperAdmin**  
   - Puede operar en contexto **global** (sin tenant o con políticas específicas de instancia).  
   - **Regla explícita:** el gate por **plan comercial** (`RequireFeature` / `HasFeatureAsync`) se aplica a **handlers que ejecutan en nombre de un tenant concreto** (tenant seleccionado o implícito en el token).  
   - Las pantallas de **configuración de instancia** (planes, cuotas, menú dinámico, empresas, etc.) **no** se bloquean por el plan de un cliente; se bloquean por **rol SuperAdmin** (y políticas de API asociadas).  
   - Si en el futuro un flujo de SuperAdmin **imita** ser un tenant (p. ej. “ver como empresa”), entonces **sí** aplican permisos y plan **de ese** tenant.

3. **Contradicción a evitar:** no exigir simultáneamente “todo formulario ligado al plan” y “SuperAdmin ve todo sin reglas” sin aclarar el contexto (global vs tenant). Esta política usa la distinción anterior.

---

## 2. Plan comercial y features (“herencia ascendente”)

1. **Definición operativa:** la disponibilidad de una funcionalidad para un tenant es **la unión** de:  
   - suscripción activa → `plan_id`;  
   - filas en **matriz de plan–feature** (`saas_plan_features`, `is_included`);  
   - **overrides** por tenant si existen.

2. **Herencia ascendente (Starter → Business → …):**  
   - Se cumple **en datos**: al definir o modificar planes en catálogo/SuperAdmin, quien administra planes **debe** incluir la feature en **todos los planes superiores** donde deba existir.  
   - **No** se asume hoy una regla automática en código del tipo “si está en Starter, infiérese en Enterprise”; si se desea más adelante, será **decisión explícita** (ADR + implementación), no parte de esta política base.

3. **Formularios nuevos:** cada comando/query sensible debe declarar **feature** o quedar **justificado** por escrito (p. ej. solo lectura global SuperAdmin) en el PR.

---

## 3. Estructura de UI: tres tabs base y excepciones

**Tabs estándar (referencia):**

| Tab            | Propósito |
|----------------|-----------|
| **Datos**      | CRUD principal (crear / editar / campos del registro). |
| **Ver + Entidad** | Vista orientada a **relaciones** del registro: listados asociados con **paginación server-side**, filtros, orden y búsqueda cuando el volumen lo justifique. |
| **Auditoría**  | Trazabilidad (quién/cuándo/qué a nivel que permita el producto: actividad, historial, etc.). |

**Excepciones:** permitidas si en el **mismo PR** se documenta: número de tabs, qué operaciones vive en cada uno, y cómo se mantiene la coherencia con el resto del sistema (referencia a pantalla similar).

**Solapamiento:** si una entidad no tiene relaciones con listado propio, el tab “Ver + Entidad” puede fusionarse con **listado general** o sustituirse por una sub-sección dentro de **Datos**, **siempre** con justificación en el PR.

---

## 4. Separación de responsabilidades (FE / BE)

- **Frontend:** permisos de UI, validación inmediata, estados de carga, mensajes de usuario, llamadas API acotadas.  
- **Backend:** autorización definitiva, validación de reglas de negocio, tenant, integridad y límites de plan.  
- Ningún tab ni botón debe confiar solo en el FE para seguridad.

---

## 5. Feedback de usuario (éxito / advertencia / error)

**Objetivo:** mensajes **tipificados** y consistentes.

**Capas (para evitar duplicidad):**

| Capa | Responsabilidad |
|------|-----------------|
| **Global (interceptor / middleware)** | Sesión inválida (p. ej. 401 → login), errores de red genéricos, opcionalmente normalizar cuerpo de error HTTP si existe convención única. |
| **Pantalla / formulario** | Éxito y error **de negocio** (validación, reglas, conflictos), advertencias contextuales. Usar el componente acordado del proyecto (**p. ej. `ZHPageNotice`**) salvo excepción justificada. |

**Regla:** no duplicar el mismo mensaje en interceptor y en pantalla para el mismo evento.

---

## 6. Validación, i18n, menú y documentación

- **Validación:** Zod (o equivalente) en UI donde haya formulario; validación y reglas de negocio en servidor siempre.  
- **i18n:** claves en `es` / `en` / `qu` (o idiomas acordados); sin texto duro en UI salvo constantes técnicas.  
- **Menú:** toda ruta de negocio nueva debe figurar en **menú dinámico o estático** según corresponda, y en **inventario de pantallas** (`docs/FRONTEND-PANTALLAS.md` o sucesor).  
- **Documentación:** OpenAPI/Swagger para contratos; ADR o sección en `docs/` para flujos y reglas de negocio no obvias.

---

## 7. SuperAdmin vs administrador de empresa (visibilidad de módulos)

- **SuperAdmin:** configura qué **planes / features / módulos** tienen los clientes y la instancia; ve las pantallas de administración global acordadas.  
- **Administrador de empresa:** ve solo los **módulos y pantallas** permitidos por **asignación (plan + permisos)** y gestiona usuarios/roles **dentro de su tenant**.  
- Cualquier pantalla nueva de “solo SuperAdmin” debe quedar explícita en rutas y políticas de API.

---

## 8. Seguridad, UX y escalabilidad (recordatorio)

- **Seguridad:** JWT, policies/claims, validación de tenant en comandos que mutan datos, entradas validadas y sanitización según estándares del stack.  
- **UX:** loaders, vacíos, confirmaciones en acciones críticas, errores visibles (alineado a §5).  
- **Listados:** paginación, filtro, orden y búsqueda **server-side** cuando el volumen o el tiempo de respuesta lo requieran; justificar en PR si se omite en fase inicial.

---

## 9. Arquitectura (CQRS, capas)

- Contratos claros: DTOs, servicios, repositorios.  
- **CQRS / comandos vs consultas:** adoptar **por módulo** cuando la complejidad o el rendimiento lo exijan; no es obligatorio en cada pantalla — documentar en ADR si se introduce.

---

## 10. Cómo se revisa esta política

- Los revisores de PR comprueban checklist breve derivado de §1–§8.  
- Cambios a esta política: PR dedicado o sección en changelog interno; versión en cabecera (`v1`, `v1.1`, …).

---

*Documento generado para revisión; no modifica código de aplicación.*

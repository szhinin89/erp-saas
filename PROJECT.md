# Proyecto — ERP SaaS ZH Technologies

## Objetivo

Construir un **sistema ERP SaaS multi-tenant** que permita a ZH Technologies comercializar, a empresas ecuatorianas de distintos tamaños, una plataforma integrada de gestión empresarial — sin que cada cliente necesite infraestructura propia ni instalaciones locales.

El producto centraliza en una sola plataforma la **facturación electrónica con el SRI de Ecuador**, el **control de inventario y compras**, la **contabilidad integrada** y la **administración de accesos por empresa**, todo segmentado por planes comerciales que el operador configura y gestiona desde un panel SuperAdmin.

El objetivo de negocio es ofrecer una alternativa SaaS accesible, extensible y adaptada a la normativa ecuatoriana, con capacidad de escalar desde una PYME con facturación básica hasta una empresa mediana con múltiples sucursales, bodegas y equipo contable.

---

## Qué es

**ERP SaaS multi-tenant** desarrollado por ZH Technologies para empresas ecuatorianas. Permite a un operador (ZH Technologies) administrar múltiples empresas desde un único panel, donde cada empresa opera de forma completamente aislada con su propio plan comercial, módulos y usuarios.

---

## Problema que resuelve

Las empresas ecuatorianas necesitan cumplir con la normativa del **SRI (Servicio de Rentas Internas)** para emitir comprobantes electrónicos, al mismo tiempo que gestionan su inventario, compras y contabilidad en un solo sistema. Las soluciones existentes son costosas, difíciles de adaptar o no cubren la integración SRI de Ecuador.

**ZH Technologies** resuelve esto con un producto SaaS que:

1. **Facturación electrónica SRI Ecuador** — emisión, autorización y gestión de comprobantes electrónicos (facturas, notas de crédito/débito, retenciones) según la normativa vigente.
2. **Control de inventario y bodegas** — stock en tiempo real, transferencias entre bodegas, ajustes, órdenes de compra con trazabilidad completa.
3. **Gestión contable integrada** — plan de cuentas, asientos automáticos al aprobar compras y ventas, configuración contable por empresa.
4. **Multi-empresa desde un solo panel** — un SuperAdmin administra N empresas (planes, módulos, usuarios, menú de navegación) sin mezclar datos entre tenants.

---

## A quién va dirigido

El producto está segmentado por plan comercial:

| Segmento | Perfil | Módulos típicos |
|----------|--------|-----------------|
| **PYME básica** | Empresa pequeña, 1-2 usuarios, facturación y catálogo | Ventas, Productos, Clientes |
| **PYME con inventario** | Empresa con bodegas, proveedores y compras | + Inventario, Compras, Proveedores |
| **Empresa mediana** | Múltiples sucursales, equipo contable, reportes | + Contabilidad, Transferencias, OC |
| **Operador** | ZH Technologies administrando instancias multi-cliente | Panel SuperAdmin completo |

---

## Modelo de negocio

- **SaaS multi-tenant:** cada empresa contrata un plan y accede a los módulos incluidos.
- **Operador único:** ZH Technologies es el SuperAdmin de la instancia; crea empresas, asigna planes y configura el menú de cada una.
- **Planes escalonados:** cada plan habilita distintos módulos (`catalog`, `accounting`, `ventas`, `compras`, etc.) y puede tener límites de uso (usuarios, clientes, etc.).
- **Despliegue flexible:** la instancia puede correr con SuperAdmin habilitado (gestión activa) o deshabilitado (solo operación por empresa), controlado por `Deployment:SuperAdminPanelEnabled`.

---

## Diferenciadores clave

- **Integración nativa SRI Ecuador** — facturación electrónica con esquema offline y (en roadmap) WSDL real con firma P12.
- **Multi-idioma desde el inicio** — español, inglés y **Kichwa de Cañar** (`qu`), alineado con la diversidad cultural ecuatoriana.
- **Arquitectura extractable** — monolito modular con Clean Architecture; cada módulo puede convertirse en microservicio sin reescribir el dominio.
- **Control granular de acceso** — permisos por módulo, recurso y acción (`perm:modulo.recurso.accion`); menú dinámico configurable por empresa desde el panel SuperAdmin.

---

## Alcance actual (MVP)

| Módulo | Estado |
|--------|--------|
| Autenticación multi-empresa + SuperAdmin | ✅ |
| Catálogo de productos, clientes, proveedores | ✅ |
| Inventario (stock, ajustes, transferencias, bodegas) | ✅ |
| Órdenes de compra + facturas de compra | ✅ |
| Ventas — facturación electrónica SRI (simulada) | ✅ backend / 🚧 frontend |
| Contabilidad (plan de cuentas, asientos, config por empresa) | ✅ |
| Caja / bancos | ✅ |
| Panel SuperAdmin (planes, menú, empresas) | ✅ |

**Pendiente para MVP comercial:**
- WSDL real SRI (firma P12 + envío + polling)
- Pantallas frontend de facturación y compras/gastos

---

## Referencias técnicas

| Documento | Contenido |
|-----------|-----------|
| `CLAUDE.md` | Reglas y convenciones de código |
| `docs/ARCHITECTURE.md` | Arquitectura del sistema |
| `docs/STATUS.md` | Estado de desarrollo detallado |
| `docs/FEATURES.md` | Funcionalidades, pantallas y endpoints |

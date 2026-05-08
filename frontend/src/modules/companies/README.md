# Companies Module - Arquitectura Modular

## 📋 Estructura

```
frontend/src/modules/companies/
├── pages/
│   └── CompaniesPage.tsx           ← Contenedor principal (delegador)
├── components/
│   ├── CompanyListPanel.tsx        ← Tabla de empresas
│   ├── CompanyDetailPanel.tsx      ← Pestaña "Datos"
│   ├── CompanyFormCard.tsx         ← Formulario de creación/edición
│   ├── GlobalParametersPanel.tsx   ← Pestaña "Parámetros Globales"
│   ├── NavigationMenuPanel.tsx     ← Pestaña "Menú de Navegación"
│   ├── AuditPanel.tsx              ← Pestaña "Auditoría"
│   └── FeatureGatePanel.tsx        ← Gateos de features SaaS
├── hooks/
│   ├── useCompanyList.ts           ← Lógica de listar empresas
│   ├── useCompanyDetail.ts         ← Lógica de detalle y edición
│   ├── useCompanyForm.ts           ← Lógica del formulario de creación
│   └── useCompanyTabs.ts           ← Control de pestañas
├── services/
│   └── companyService.ts           ← Reutilizable (ya existe)
└── index.ts                        ← Exporta página principal
```

## 🎯 Migración desde `/pages/CompaniesPage.tsx`

**ANTES** (782 líneas en un archivo):
- Lógica de lista
- Lógica de detalle
- Lógica de formulario
- Lógica de tablas
- 50+ useState hooks
- Múltiples side effects

**DESPUÉS** (Modular):
- `CompaniesPage.tsx`: 50-100 líneas (delegador de componentes)
- `useCompanyList.ts`: Lógica de lista reutilizable
- `useCompanyDetail.ts`: Lógica de edición reutilizable
- `useCompanyForm.ts`: Lógica de formulario reutilizable
- Componentes visuales: Cada uno 100-150 líneas

## 🔄 Custom Hooks Pattern

### useCompanyList
```typescript
const {
  items,
  loading,
  error,
  searchQuery,
  setSearchQuery,
  refreshList,
} = useCompanyList();
```

### useCompanyDetail
```typescript
const {
  detail,
  loading,
  error,
  isSaving,
  save,
  refresh,
  setDetailTenantId,
} = useCompanyDetail();
```

### useCompanyForm
```typescript
const {
  form,
  isSubmitting,
  onSubmit,
  reset,
} = useCompanyForm(onSuccess);
```

### useCompanyTabs
```typescript
const {
  currentTab,
  setTab,
  tabs,
} = useCompanyTabs('data');
```

## 📦 Exportación

Crear `index.ts` para simplificar imports:
```typescript
export { default as CompaniesPage } from './pages/CompaniesPage';
export * from './components';
export * from './hooks';
export * from './services';
```

## ✅ Beneficios

1. **Mantenibilidad**: Cada componente ~150 líneas
2. **Testabilidad**: Hooks separados = test unitarios
3. **Reutilización**: Hooks usables en otras páginas
4. **Performance**: Componentes se re-renderizan solo si props cambian
5. **Colaboración**: Múltiples devs pueden trabajar en paralelo

## 🚀 Próximos Pasos

1. Extraer hooks de CompaniesPage.tsx actual
2. Crear componentes visuales
3. Mover CompaniesPage al módulo
4. Actualizar import en App.tsx
5. Eliminar /pages/CompaniesPage.tsx antiguo


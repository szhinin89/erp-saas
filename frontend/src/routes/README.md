# Rutas — sistema modular

## 📋 Estructura

```
frontend/src/routes/
├── publicRoutes.tsx          ← Rutas públicas (sin autenticación)
├── adminRoutes.tsx           ← SuperAdmin panel (condicional)
├── mainRoutes.tsx            ← Dashboard, Productos, Ventas, Contabilidad
├── catalogRoutes.tsx         ← Módulo Catalog (Brands, Units, Tax Rates, etc.)
├── companiesRoutes.tsx       ← Módulo Companies (SaaS)
├── accessRoutes.tsx          ← Security, Access, Profiles
├── index.ts                  ← Composición principal
└── README.md                 ← Este archivo
```

## 🎯 Patrón: Rutas Descentralizadas

### ANTES (monolítico):
```typescript
// App.tsx con 40+ rutas en un solo archivo
function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/products" element={<ProductsPage />} />
      <Route path="/inventario/brands" element={<BrandsCatalogPage />} />
      <Route path="/companies" element={<CompaniesPage />} />
      {/* ... 36 rutas más ... */}
    </Routes>
  );
}
```

### DESPUÉS (modular):
```typescript
// App.tsx limpio
function App() {
  return (
    <ConfigProvider>
      <AppRoutes />
    </ConfigProvider>
  );
}

// AppRoutes.tsx (nuevo)
function AppRoutes() {
  const { superAdminPanelEnabled } = useDeployment();
  const routes = getAppRoutes({ superAdminPanelEnabled });
  
  return (
    <BrowserRouter>
      <Routes>
        {/* Rutas públicas */}
        {publicRoutes}
        
        <Route element={<ProtectedRoute />}>
          <Route element={<AppLayout />}>
            {/* Rutas protegidas */}
            {adminRoutes(superAdminPanelEnabled)}
            {mainRoutes}
            {catalogRoutes}
            {companiesRoutes}
            {accessRoutes}
          </Route>
        </Route>
        
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
```

## 🔄 Agregar Nuevas Rutas

### 1. Crear archivo de rutas por módulo
```typescript
// routes/myModuleRoutes.tsx
export const myModuleRoutes = [
  <Route key="my-page" path="/my-module/page" element={<MyPage />} />,
];
```

### 2. Exportar en `index.ts`
```typescript
export { myModuleRoutes } from './myModuleRoutes';

// Y agregar a getAppRoutes()
export function getAppRoutes(config: AppRoutesConfig) {
  return [
    // ... otras rutas
    ...myModuleRoutes,  // ← Nueva
  ];
}
```

### 3. Usar en App.tsx o componente routing
```typescript
import { getAppRoutes } from './routes';

function AppRoutes() {
  const routes = getAppRoutes(config);
  return (
    <Routes>
      {routes}
    </Routes>
  );
}
```

## ✅ Convenciones

1. **Nombres**: `xyzRoutes.tsx` (kebab-case para rutas, camelCase para variables)
2. **Exportación**: `export const xyzRoutes = [...]` (array de Route elements)
3. **Keys únicos**: Cada Route tiene un `key` único para React
4. **Comentarios**: Documentar las rutas en comentarios JSDoc
5. **Lógica condicional**: Si la ruta depende de un flag, usar función `function xyzRoutes(enabled)`

## 🚀 Beneficios

1. **Mantenibilidad**: Cada módulo controla sus rutas
2. **Escalabilidad**: Agregar nuevas rutas sin tocacer App.tsx
3. **Testabilidad**: Rutas pueden testearse independientemente
4. **Versionabilidad**: Si un módulo tiene versiones, sus rutas también
5. **Colaboración**: Múltiples devs pueden agregar rutas sin conflictos

## 📦 Próximos Pasos

1. ✅ Crear estructura de rutas modular
2. [ ] Actualizar App.tsx para usar `getAppRoutes()`
3. [ ] Aplicar el patrón a futuras rutas
4. [ ] Considerar lazy-loading de módulos con `React.lazy()`


/**
 * Extensión futura: facturación y otros módulos pueden reutilizar el mismo cliente.
 *
 * - UI de campos: `import { CustomerFormFields } from '../../components/catalog/CustomerFormFields'`
 * - Validación / tipo de formulario: `customerCatalogFormSchema`, `CustomerCatalogFormValues` desde
 *   `modules/customers/schemas/customerCatalogFormSchema`
 * - Valores por defecto y mapeo API: `emptyCustomerForm`, `customerFormFromDto`, `customerFormToApiBody` desde
 *   `modules/catalog/customers/customerFormModel`
 *
 * Flujo típico en una futura pantalla “Nueva factura”:
 * 1. `useForm({ resolver: zodResolver(customerCatalogFormSchema), defaultValues: emptyCustomerForm() })`
 * 2. Opcional: `reset(customerFormFromDto(clienteSeleccionado))` al elegir cliente existente
 * 3. Tras validar: `customerCatalogService.create(customerFormToApiBody(data))` o update según negocio
 *
 * No importar desde aquí en runtime hasta existir el módulo de facturación; este archivo es ancla de convención.
 */
export const BILLING_CUSTOMER_FORM_CONVENTION = 'reuse: CustomerFormFields + customerFormModel + customerFormSchema';

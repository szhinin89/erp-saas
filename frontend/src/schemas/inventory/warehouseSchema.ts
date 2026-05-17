import { z } from 'zod';

export const STORAGE_TYPES = ['Mixto', 'Frío', 'Seco', 'Granel'] as const;

export const warehouseSchema = z.object({
  branchId:         z.string().min(1, 'Seleccione una sucursal.'),
  name:             z.string().min(1, 'El nombre es requerido.'),
  storageType:      z.string().optional(),
  address:          z.string().optional(),
  phone:            z.string().optional(),
  email:            z.string().trim().optional().or(z.literal('')).refine(
    (v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v),
    'Correo inválido',
  ),
  manager:          z.string().optional(),
  latitude:         z.string().optional(),
  longitude:        z.string().optional(),
  capacity:         z.coerce.number().min(0).optional().nullable(),
  dailyDispatchGoal: z.coerce.number().min(0).optional().nullable(),
});

export type WarehouseFormValues = z.infer<typeof warehouseSchema>;

export const defaultWarehouseValues: WarehouseFormValues = {
  branchId: '', name: '', storageType: '', address: '',
  phone: '', email: '', manager: '', latitude: '', longitude: '',
  capacity: null, dailyDispatchGoal: null,
};

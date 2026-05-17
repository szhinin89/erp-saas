import { z } from 'zod';

const sriCode3 = z
  .string()
  .regex(/^\d{3}$/, 'Debe ser exactamente 3 dígitos numéricos (ej: 001)');

export const sriConfigSchema = z.object({
  ruc:                z.string().regex(/^\d{13}$/, 'El RUC debe tener exactamente 13 dígitos'),
  legalName:          z.string().min(1, 'La razón social es obligatoria').max(200),
  tradeName:          z.string().max(200).optional(),
  mainAddress:        z.string().min(1, 'La dirección matriz es obligatoria').max(500),
  requiresAccounting: z.boolean(),
  specialTaxpayer:    z.string().max(200).optional(),
  estabCode:          sriCode3,
  emPointCode:        sriCode3,
  certP12Path:        z.string().min(1, 'La ruta del certificado .p12 es obligatoria').max(500),
  certPassword:       z.string().optional(),
  environment:        z.number().int().refine((v) => v === 1 || v === 2, 'Selecciona un ambiente válido'),
  emissionType:       z.number().int().default(1),
  wsdlUrl:            z.string().url('Debe ser una URL válida').max(500),
});

export type SriConfigValues = z.infer<typeof sriConfigSchema>;

export const SRI_WSDL_DEFAULTS = {
  pruebas:    'https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl',
  produccion: 'https://cel.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl',
};

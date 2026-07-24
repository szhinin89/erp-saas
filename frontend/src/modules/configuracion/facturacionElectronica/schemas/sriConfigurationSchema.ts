import { z } from 'zod';

export const sriConfigSchema = z.object({
  certPassword: z.string().optional(),
  environment:  z.union([z.literal(1), z.literal(2)]),
  wsdlUrl:      z.string().url('Debe ser una URL válida').max(500),
});

export type SriConfigValues = z.infer<typeof sriConfigSchema>;

export const SRI_WSDL_DEFAULTS = {
  pruebas:    'https://celcer.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl',
  produccion: 'https://cel.sri.gob.ec/comprobantes-electronicos-ws/RecepcionComprobantesOffline?wsdl',
};

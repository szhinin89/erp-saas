import { z } from 'zod';

export const companyOperationSchema = z.object({
  languageCode: z.enum(['es', 'en', 'qu']),
});

export type CompanyOperationValues = z.infer<typeof companyOperationSchema>;

export const defaultCompanyOperationValues: CompanyOperationValues = {
  languageCode: 'es',
};

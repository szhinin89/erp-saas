import { api } from '../../lib/api';
import type { ApiResponse } from '../../../types/api';

export type OnboardingContextDto = {
  companyId: string;
  onboardingCompleted: boolean;
  operationalStatus: string;
  currentTaxId: string;
  isProvisionalTaxId: boolean;
  currentLegalName: string;
  currentAddress: string;
  currentEmail: string | null;
  currentPhone: string | null;
  subscriberName: string | null;
  preferredLanguage: string;
};

export type CompleteOnboardingRequest = {
  useSubscriberData: boolean;
  taxId: string;
  legalName: string;
  tradeName?: string | null;
  mainAddress: string;
  phone?: string | null;
  email?: string | null;
  createDefaultBranch: boolean;
};

export type CompanyOnboardingResultDto = {
  companyId: string;
  taxId: string;
  legalName: string;
  status: string;
};

export const onboardingService = {
  async getContext(): Promise<OnboardingContextDto> {
    const { data } = await api.get<ApiResponse<OnboardingContextDto>>('/api/onboarding/company');
    if (!data.responseObject) throw new Error('No se pudo obtener el contexto de onboarding.');
    return data.responseObject;
  },

  async complete(request: CompleteOnboardingRequest): Promise<CompanyOnboardingResultDto> {
    const { data } = await api.post<ApiResponse<CompanyOnboardingResultDto>>(
      '/api/onboarding/company/complete',
      request,
    );
    if (!data.responseObject) throw new Error('Error al completar el onboarding.');
    return data.responseObject;
  },
};

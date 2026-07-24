import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';
import type {
  CompanyBrandingIdentity,
  CompanyProfile,
  UpdateCompanyDocumentsPayload,
  UpdateCompanyFiscalPayload,
  UpdateCompanyOperationPayload,
  UpdateCompanyProfilePayload,
} from '../../../../types/companyProfile';

export const companyProfileService = {
  async getProfile(): Promise<CompanyProfile | null> {
    const { data } = await api.get<ApiResponse<CompanyProfile>>('/api/v1/companies/profile');
    return data.data ?? null;
  },

  async updateProfile(payload: UpdateCompanyProfilePayload): Promise<CompanyProfile> {
    const { data } = await api.put<ApiResponse<CompanyProfile>>('/api/v1/companies/profile', payload);
    return data.data;
  },

  async uploadLogo(file: File, onProgress?: (percent: number) => void): Promise<CompanyProfile> {
    const formData = new FormData();
    formData.append('file', file);

    const { data } = await api.post<ApiResponse<CompanyProfile>>('/api/v1/companies/profile/logo', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (event) => {
        if (!onProgress || !event.total) return;
        onProgress(Math.round((event.loaded / event.total) * 100));
      },
    });
    return data.data;
  },

  async getLogoBlob(): Promise<Blob | null> {
    try {
      const { data } = await api.get<Blob>('/api/v1/companies/profile/logo/content', {
        responseType: 'blob',
      });
      return data;
    } catch (err) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 404) return null;
      throw err;
    }
  },

  async updateFiscal(payload: UpdateCompanyFiscalPayload): Promise<CompanyProfile> {
    const { data } = await api.put<ApiResponse<CompanyProfile>>('/api/v1/companies/profile/fiscal', payload);
    return data.data;
  },

  async updateOperation(payload: UpdateCompanyOperationPayload): Promise<CompanyProfile> {
    const { data } = await api.put<ApiResponse<CompanyProfile>>('/api/v1/companies/profile/operation', payload);
    return data.data;
  },

  async updateDocuments(payload: UpdateCompanyDocumentsPayload): Promise<CompanyProfile> {
    const { data } = await api.put<ApiResponse<CompanyProfile>>('/api/v1/companies/profile/documents', payload);
    return data.data;
  },

  async updateBranding(identity: CompanyBrandingIdentity): Promise<CompanyProfile> {
    const { data } = await api.put<ApiResponse<CompanyProfile>>('/api/v1/companies/profile/branding', {
      brandingConfiguration: JSON.stringify(identity),
    });
    return data.data;
  },

  async uploadLogoAlt(file: File, onProgress?: (percent: number) => void): Promise<CompanyProfile> {
    const formData = new FormData();
    formData.append('file', file);

    const { data } = await api.post<ApiResponse<CompanyProfile>>('/api/v1/companies/profile/logo-alt', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (event) => {
        if (!onProgress || !event.total) return;
        onProgress(Math.round((event.loaded / event.total) * 100));
      },
    });
    return data.data;
  },

  async getLogoAltBlob(): Promise<Blob | null> {
    try {
      const { data } = await api.get<Blob>('/api/v1/companies/profile/logo-alt/content', {
        responseType: 'blob',
      });
      return data;
    } catch (err) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 404) return null;
      throw err;
    }
  },
};

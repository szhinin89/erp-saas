import { apiGet, apiPatch, apiPost, apiPut } from '../../lib/apiEnvelope';
import type { AttributeGroupDto, AttributeDefinitionDto } from '../../../types/items';
import type { BrandDto } from '../../../types/catalog';

// Re-export brands from the existing catalog service to avoid duplication
export { catalogService as brandService } from '../../catalog/api/catalogService';

export interface CreateAttributeGroupRequest {
  code: string;
  name: string;
  sortOrder?: number;
}

export interface CreateAttributeDefinitionRequest {
  groupId: string;
  code: string;
  name: string;
  dataType: string;
  isVariantAxis?: boolean;
  allowedValues?: string | null;
  isRequired?: boolean;
  sortOrder?: number;
}

export const catalogItemService = {
  // ── Attribute Groups ────────────────────────────────────────────────────
  getAttributeGroups: () =>
    apiGet<AttributeGroupDto[]>('/api/catalog/attribute-groups').then((d) => d ?? []),

  createAttributeGroup: (request: CreateAttributeGroupRequest) =>
    apiPost<AttributeGroupDto>('/api/catalog/attribute-groups', request),

  updateAttributeGroup: (id: string, request: { name: string; sortOrder: number }) =>
    apiPut<AttributeGroupDto>(`/api/catalog/attribute-groups/${id}`, { id, ...request }),

  disableAttributeGroup: (id: string) =>
    apiPatch<boolean>(`/api/catalog/attribute-groups/${id}/disable`),

  // ── Attribute Definitions ───────────────────────────────────────────────
  getAttributeDefinitions: (groupId?: string) => {
    const qs = groupId ? `?groupId=${groupId}` : '';
    return apiGet<AttributeDefinitionDto[]>(`/api/catalog/attribute-definitions${qs}`).then((d) => d ?? []);
  },

  createAttributeDefinition: (request: CreateAttributeDefinitionRequest) =>
    apiPost<AttributeDefinitionDto>('/api/catalog/attribute-definitions', request),

  disableAttributeDefinition: (id: string) =>
    apiPatch<boolean>(`/api/catalog/attribute-definitions/${id}/disable`),
};

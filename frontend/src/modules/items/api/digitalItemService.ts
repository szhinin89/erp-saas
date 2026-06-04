import { apiGet, apiPost } from '../../lib/apiEnvelope';
import type { DigitalItemDto, DeliverableDto } from '../../../types/items';

export interface CreateDigitalItemRequest {
  itemId: string;
  licenseType: string;
  deliveryMethod: string;
  maxDownloads?: number | null;
  validityDays?: number | null;
}

export const digitalItemService = {
  getByItem: (itemId: string) =>
    apiGet<DigitalItemDto>(`/api/digital-items/by-item/${itemId}`),

  create: (request: CreateDigitalItemRequest) =>
    apiPost<DigitalItemDto>('/api/digital-items', request),

  addDeliverable: (id: string, request: { name: string; storageObjectId: string; version?: string }) =>
    apiPost<DeliverableDto>(`/api/digital-items/${id}/deliverables`, request),
};

import { describe, it, expect, vi, beforeEach } from 'vitest';

const apiGetMock = vi.fn();
const apiPostMock = vi.fn();
const apiGetBlobMock = vi.fn();

vi.mock('../../lib/apiEnvelope', () => ({
  apiGet: (...args: unknown[]) => apiGetMock(...args),
  apiPost: (...args: unknown[]) => apiPostMock(...args),
}));

vi.mock('../../lib/api', () => ({
  api: { get: (...args: unknown[]) => apiGetBlobMock(...args) },
}));

import { rideService } from './rideService';

describe('rideService', () => {
  beforeEach(() => {
    apiGetMock.mockReset();
    apiPostMock.mockReset();
    apiGetBlobMock.mockReset();
  });

  it('getOrGenerate calls GET /api/v1/ride with sourceModule/sourceEntityId as query params', async () => {
    apiGetMock.mockResolvedValue({ outcome: 'Generated', storagePath: 'x', metadata: null, reasonCode: null });

    const result = await rideService.getOrGenerate('Sales', 'abc-123');

    expect(apiGetMock).toHaveBeenCalledWith('/api/v1/ride', {
      params: { sourceModule: 'Sales', sourceEntityId: 'abc-123' },
    });
    expect(result.outcome).toBe('Generated');
  });

  it('regenerate calls POST /api/v1/ride/regenerate with the body', async () => {
    apiPostMock.mockResolvedValue({ outcome: 'Generated', storagePath: 'x', metadata: null, reasonCode: null });

    await rideService.regenerate('Sales', 'abc-123');

    expect(apiPostMock).toHaveBeenCalledWith('/api/v1/ride/regenerate', {
      sourceModule: 'Sales', sourceEntityId: 'abc-123',
    });
  });

  it('getContentBlob calls the raw axios instance with responseType blob and returns the blob', async () => {
    const blob = new Blob(['pdf-bytes'], { type: 'application/pdf' });
    apiGetBlobMock.mockResolvedValue({ data: blob });

    const result = await rideService.getContentBlob('Sales', 'abc-123');

    expect(apiGetBlobMock).toHaveBeenCalledWith('/api/v1/ride/content', {
      params: { sourceModule: 'Sales', sourceEntityId: 'abc-123' },
      responseType: 'blob',
    });
    expect(result).toBe(blob);
  });
});

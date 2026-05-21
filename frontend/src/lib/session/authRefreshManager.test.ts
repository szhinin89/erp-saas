import { afterEach, describe, expect, it, vi } from 'vitest';
import axios from 'axios';
import {
  __testHandleAuthBroadcast,
  __testResetAuthRefreshManager,
  refreshSessionToken,
} from './authRefreshManager';
import { clearAccessToken, getAccessToken, setAccessToken } from './authTokenMemory';

vi.mock('axios', () => ({
  default: { post: vi.fn() },
}));

vi.mock('../../store/authStore', () => ({
  useAuthStore: {
    getState: () => ({ updateTokens: vi.fn() }),
  },
}));

describe('authRefreshManager', () => {
  afterEach(() => {
    __testResetAuthRefreshManager();
    clearAccessToken();
    vi.mocked(axios.post).mockReset();
  });

  it('deduplica llamadas concurrentes en una sola petición HTTP', async () => {
    vi.mocked(axios.post).mockImplementation(
      () =>
        new Promise((resolve) => {
          setTimeout(
            () =>
              resolve({
                data: { responseObject: { token: 'access-1' } },
              }),
            30,
          );
        }),
    );

    const [a, b] = await Promise.all([refreshSessionToken(), refreshSessionToken()]);

    expect(a).toBe('access-1');
    expect(b).toBe('access-1');
    expect(axios.post).toHaveBeenCalledTimes(1);
  });

  it('aplica access token recibido por BroadcastChannel de otra pestaña', async () => {
    __testHandleAuthBroadcast({
      type: 'refresh_success',
      tabId: 'other-tab',
      accessToken: 'remote-token',
    });

    expect(getAccessToken()).toBe('remote-token');
  });

  it('reintenta una vez en bootstrapRetry tras 401 ya utilizado', async () => {
    vi.mocked(axios.post)
      .mockRejectedValueOnce({
        response: { status: 401, data: { message: 'Refresh token ya utilizado.' } },
      })
      .mockResolvedValueOnce({
        data: { responseObject: { token: 'after-retry' } },
      });

    const token = await refreshSessionToken({ bootstrapRetry: true });
    expect(token).toBe('after-retry');
    expect(axios.post).toHaveBeenCalledTimes(2);
  });

  it('devuelve token en memoria sin POST si ya existe', async () => {
    setAccessToken('cached');
    const token = await refreshSessionToken();
    expect(token).toBe('cached');
    expect(axios.post).not.toHaveBeenCalled();
  });
});

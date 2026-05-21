import { afterEach, describe, expect, it, vi } from 'vitest';
import axios from 'axios';
import { refreshSessionToken, resetRefreshSessionFlight } from './refreshSessionToken';
import { clearAccessToken } from './authTokenMemory';

vi.mock('axios', () => ({
  default: { post: vi.fn() },
}));

vi.mock('../../store/authStore', () => ({
  useAuthStore: {
    getState: () => ({ updateTokens: vi.fn() }),
  },
}));

describe('refreshSessionToken', () => {
  afterEach(() => {
    resetRefreshSessionFlight();
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
});

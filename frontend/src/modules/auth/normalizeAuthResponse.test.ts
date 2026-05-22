import { describe, expect, it } from 'vitest';
import { normalizeAuthResponse } from './normalizeAuthResponse';

describe('normalizeAuthResponse', () => {
  it('maps PascalCase API fields to AuthResponse', () => {
    const session = normalizeAuthResponse({
      UserId: 'u1',
      FullName: 'Admin',
      Email: 'a@x.com',
      Role: 'SuperAdmin',
      SubscriberId: 's1',
      Token: 'jwt-here',
      PlanCode: 'starter',
      EnabledModules: ['sales'],
    });

    expect(session.userId).toBe('u1');
    expect(session.token).toBe('jwt-here');
    expect(session.subscriberId).toBe('s1');
    expect(session.enabledModules).toEqual(['sales']);
  });
});

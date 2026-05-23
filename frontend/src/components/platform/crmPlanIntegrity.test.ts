import { describe, expect, it } from 'vitest';
import {
  buildPlanFromDraft,
  collectTreeNodeIds,
  normalizeCrmPlans,
  normalizePlanActiveById,
  parseImportedCrmWorkspace,
  resolveInheritedActiveNodeIds,
  type CrmPlanModel,
} from './crmPlanIntegrity';

const fallbackPlans: CrmPlanModel[] = [
  {
    id: 'starter',
    code: 'STARTER',
    name: 'STARTER',
    priceMonthly: 49,
    priceYearly: 470,
    description: 'Fallback',
    layout: 'horizontal',
    highlight: false,
  },
];

describe('crmPlanIntegrity', () => {
  it('normaliza planes corruptos desde localStorage', () => {
    const normalized = normalizeCrmPlans(
      [
        { id: 'plan', code: 'pro', name: 'pro', priceMonthly: 99, layout: 'vertical' },
        { id: 'plan', code: 'pro', name: 'pro plus', priceMonthly: -50, priceYearly: 'abc' },
      ],
      fallbackPlans,
    );

    expect(normalized).toHaveLength(2);
    expect(normalized[0]?.id).toBe('plan');
    expect(normalized[0]?.code).toBe('PRO');
    expect(normalized[0]?.layout).toBe('vertical');
    expect(normalized[1]?.id).toBe('plan_2');
    expect(normalized[1]?.code).toBe('PRO_2');
    expect(normalized[1]?.priceMonthly).toBe(0);
    expect(normalized[1]?.priceYearly).toBe(0);
  });

  it('filtra activaciones inválidas por plan y nodo', () => {
    const clean = normalizePlanActiveById(
      {
        valid: ['a', 'a', 'b', 'x'],
        stalePlan: ['a'],
        malformed: 'oops',
      },
      new Set(['valid']),
      new Set(['a', 'b']),
    );

    expect(clean).toEqual({ valid: ['a', 'b'] });
  });

  it('crea un plan válido y único desde draft', () => {
    const result = buildPlanFromDraft(
      {
        name: 'Pro',
        monthly: '120',
        yearly: '',
        description: '',
      },
      [
        {
          id: 'plan_123',
          code: 'PRO',
          name: 'PRO',
          priceMonthly: 50,
          priceYearly: 500,
          description: '',
          layout: 'horizontal',
          highlight: false,
        },
      ],
      123,
    );

    expect('plan' in result).toBe(true);
    if ('plan' in result) {
      expect(result.plan.id).toBe('plan_123_2');
      expect(result.plan.code).toBe('PRO_2');
      expect(result.plan.priceYearly).toBe(Math.round(120 * 12 * 0.8));
    }
  });

  it('hereda activaciones válidas sin duplicar', () => {
    const inherited = resolveInheritedActiveNodeIds(
      { source: ['a', 'a', 'b', 'x'] },
      'source',
      new Set(['a', 'b', 'c']),
    );
    expect(inherited).toEqual(['a', 'b']);
  });

  it('extrae ids del árbol de forma recursiva', () => {
    const ids = collectTreeNodeIds([
      { uid: 'root', children: [{ uid: 'child', children: [{ uid: 'leaf', children: [] }] }] },
      { uid: 'root', children: [] },
    ]);
    expect(Array.from(ids)).toEqual(['root', 'child', 'leaf']);
  });

  it('importa snapshot y sanea planes/activaciones/auditoría', () => {
    const imported = parseImportedCrmWorkspace(
      {
        version: 3,
        exportedAt: '2026-01-01T00:00:00.000Z',
        plans: [{ id: 'p1', code: 'pro', name: 'Pro', priceMonthly: 100, priceYearly: 900 }],
        tree: [{ uid: 'n1', children: [{ uid: 'n2', children: [] }] }],
        planActiveById: { p1: ['n1', 'n2', 'n3'] },
        auditLines: [' one ', '', 1, 'two'],
      },
      fallbackPlans,
    );

    expect(imported.version).toBe(3);
    expect(imported.plans).toHaveLength(1);
    expect(imported.plans[0]?.code).toBe('PRO');
    expect(imported.planActiveById).toEqual({ p1: ['n1', 'n2'] });
    expect(imported.auditLines).toEqual(['one', 'two']);
  });
});

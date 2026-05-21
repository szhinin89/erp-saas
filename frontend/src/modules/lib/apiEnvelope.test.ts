import { describe, expect, it } from 'vitest';
import { readEnvelopePayload } from './apiEnvelope';

describe('readEnvelopePayload', () => {
  it('extrae responseObject en camelCase', () => {
    expect(readEnvelopePayload<{ id: string }>({ responseObject: { id: '1' } })).toEqual({ id: '1' });
  });

  it('extrae ResponseObject en PascalCase', () => {
    expect(readEnvelopePayload<number>({ ResponseObject: 42 })).toBe(42);
  });

  it('devuelve el body si no hay envelope', () => {
    expect(readEnvelopePayload<string>('plain')).toBe('plain');
  });
});

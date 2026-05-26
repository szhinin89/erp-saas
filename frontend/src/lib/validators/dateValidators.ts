import { z } from 'zod';
import { isValidIsoDate } from '../formatters/dateFormatters';

export const isoDateSchema = z
  .string()
  .min(1, 'validation.date.required')
  .refine(isValidIsoDate, 'validation.date.invalid');

export const isoDateOptionalSchema = z
  .string()
  .optional()
  .refine((v) => !v || isValidIsoDate(v), 'validation.date.invalid');

export const isoDateNotFutureSchema = isoDateSchema.refine(
  (v) => new Date(v) <= new Date(),
  'validation.date.notFuture',
);

export const isoDateNotPastSchema = isoDateSchema.refine(
  (v) => new Date(v) >= new Date(new Date().toISOString().split('T')[0]!),
  'validation.date.notPast',
);

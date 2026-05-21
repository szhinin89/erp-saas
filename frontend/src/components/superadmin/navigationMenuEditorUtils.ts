export function parseNavRowKey(key: string | null): { groupId: string; itemId: string } | null {
  if (!key) return null;
  const parts = key.split('::');
  if (parts.length !== 2 || !parts[0] || !parts[1]) return null;
  return { groupId: parts[0]!, itemId: parts[1]! };
}

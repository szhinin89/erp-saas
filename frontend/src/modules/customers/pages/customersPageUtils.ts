export type CustomerTabId = 'clientes' | 'categorias' | 'contactos' | 'auditoria';

export type ContactItem = {
  id: string;
  customerId: string;
  name: string;
  role: string;
  email: string;
  phone: string;
};

export type CustomerCategory = 'Corporativo' | 'Minorista' | 'Gobierno';

export type Categorization = {
  category: CustomerCategory;
  tags: string;
};

export type AuditItem = {
  id: string;
  at: string;
  user: string;
  action: string;
  customerName: string;
  details: string;
};

export function buildId() {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  return `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

export function getInitials(name: string): string {
  const words = name.trim().split(/\s+/);
  if (words.length === 0) return '?';
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[1][0]).toUpperCase();
}

export function categoryBadgeClass(category: CustomerCategory): string {
  if (category === 'Corporativo') return 'badge badge--blue';
  if (category === 'Gobierno') return 'badge badge--orange';
  return 'badge badge--gray';
}

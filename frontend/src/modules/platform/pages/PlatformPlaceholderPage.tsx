import { useLocation } from 'react-router-dom';
import { PlatformCrudTemplate } from '../../../templates/PlatformCrudTemplate';

const COPY: Record<string, { title: string; subtitle: string }> = {
  users: {
    title: 'Platform users',
    subtitle: 'Gestión de operadores platform y Support. UI en construcción — API canónica: GET /api/platform/users.',
  },
  billing: {
    title: 'Platform billing',
    subtitle: 'Facturación SaaS agregada del control plane. UI en construcción — use impersonación hacia /saas/billing por suscriptor.',
  },
};

type Props = { section?: keyof typeof COPY };

export function PlatformPlaceholderPage({ section }: Props) {
  const location = useLocation();
  const key = section ?? (location.pathname.split('/').pop() as keyof typeof COPY) ?? 'users';
  const copy = COPY[key] ?? COPY.users;

  return (
    <PlatformCrudTemplate title={copy.title} subtitle={copy.subtitle}>
      <p className="subtle">
        Esta pantalla forma parte del shell canónico del panel platform. La funcionalidad legacy sigue disponible vía
        impersonación o APIs platform hasta completar la migración.
      </p>
    </PlatformCrudTemplate>
  );
}

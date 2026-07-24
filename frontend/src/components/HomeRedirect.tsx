import { Navigate } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';

export function HomeRedirect() {
  const user = useAuthStore((s) => s.user);

  if (user?.tenantId) {
    return <Navigate to="/dashboard" replace />;
  }

  return <Navigate to="/login" replace />;
}

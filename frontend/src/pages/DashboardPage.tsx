import { useAuthStore } from '../store/authStore';
import './DashboardPage.css';

const stats = [
  { label: 'Productos', value: '—', icon: '📦' },
  { label: 'Asientos contables', value: '—', icon: '📒' },
  { label: 'Cuentas', value: '—', icon: '🏦' },
  { label: 'Usuarios', value: '—', icon: '👤' },
];

export function DashboardPage() {
  const user = useAuthStore((s) => s.user);

  return (
    <div className="dashboard">
      <div className="page-header">
        <h1 className="page-title">Dashboard</h1>
        <p className="page-subtitle">Bienvenido, {user?.fullName}</p>
      </div>

      <div className="stats-grid">
        {stats.map((s) => (
          <div key={s.label} className="stat-card">
            <span className="stat-icon">{s.icon}</span>
            <div className="stat-body">
              <span className="stat-value">{s.value}</span>
              <span className="stat-label">{s.label}</span>
            </div>
          </div>
        ))}
      </div>

      <div className="info-card">
        <h2>Información de sesión</h2>
        <dl className="info-grid">
          <dt>Usuario</dt>
          <dd>{user?.fullName}</dd>
          <dt>Correo</dt>
          <dd>{user?.email}</dd>
          <dt>Rol</dt>
          <dd>{user?.role}</dd>
          <dt>Tenant ID</dt>
          <dd className="mono">{user?.tenantId}</dd>
        </dl>
      </div>
    </div>
  );
}

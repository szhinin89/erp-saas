import React from 'react';

/**
 * Shown when the backend returns 403 with code=subscription_suspended/inactive/cancelled.
 * The user is already logged out at this point (session cleared).
 */
export function SubscriptionSuspendedPage() {
  const params  = new URLSearchParams(window.location.search);
  const code    = params.get('code')    ?? 'subscription_suspended';
  const reason  = params.get('reason')  ?? '';

  const title = {
    subscription_suspended    : 'Cuenta suspendida',
    subscription_inactive     : 'Cuenta inactiva',
    subscription_cancelled    : 'Suscripción cancelada',
    subscription_trial_expired: 'Período de prueba vencido',
    subscription_access_denied: 'Acceso denegado',
  }[code] ?? 'Acceso denegado';

  const description = {
    subscription_suspended    : 'Tu cuenta ha sido suspendida. Contacta al soporte para reactivarla.',
    subscription_inactive     : 'Tu cuenta está inactiva.',
    subscription_cancelled    : 'Tu suscripción ha sido cancelada.',
    subscription_trial_expired: 'Tu período de prueba ha vencido. Actualiza tu plan para continuar.',
    subscription_access_denied: 'No tienes acceso a la plataforma en este momento.',
  }[code] ?? 'Contacta al soporte para más información.';

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      padding: '2rem',
      textAlign: 'center',
      fontFamily: 'system-ui, sans-serif',
      background: '#f8f9fa',
    }}>
      <div style={{
        maxWidth: 480,
        background: '#fff',
        borderRadius: 12,
        padding: '2.5rem',
        boxShadow: '0 2px 16px rgba(0,0,0,.08)',
      }}>
        <div style={{ fontSize: 48, marginBottom: '1rem' }}>🔒</div>
        <h1 style={{ margin: '0 0 .75rem', fontSize: '1.5rem', color: '#1a1a1a' }}>{title}</h1>
        <p style={{ margin: '0 0 .5rem', color: '#555' }}>{description}</p>
        {reason && (
          <p style={{
            margin: '.75rem 0 0',
            fontSize: '.85rem',
            color: '#888',
            background: '#f5f5f5',
            padding: '.5rem .75rem',
            borderRadius: 6,
          }}>
            {reason}
          </p>
        )}
        <button
          onClick={() => { window.location.href = '/login'; }}
          style={{
            marginTop: '1.75rem',
            padding: '.625rem 1.5rem',
            background: '#1F3864',
            color: '#fff',
            border: 'none',
            borderRadius: 8,
            fontSize: '1rem',
            cursor: 'pointer',
          }}
        >
          Ir al inicio de sesión
        </button>
      </div>
    </div>
  );
}

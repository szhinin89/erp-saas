/** Deterministic QA seed manifest — fixed credentials for reproducible CI runs. */
export const SEED_MANIFEST = {
  superAdmin: {
    firstName: 'Super',
    lastName: 'Admin',
    email: 'superadmin@qa-engine.local',
    password: 'QaEngineSuperAdmin123!',
  },
  tenantA: {
    subscriberName: 'Subscriber Demo',
    subscriberSlug: 'subscriber-demo',
    adminFirstName: 'Admin',
    adminLastName: 'ERP',
    adminEmail: 'admin@erp.com',
    adminPassword: 'Admin123!',
    planCode: 'starter',
  },
  tenantB: {
    subscriberName: 'Tenant B QA',
    subscriberSlug: 'tenant-b-qa',
    adminFirstName: 'Admin',
    adminLastName: 'TenantB',
    adminEmail: 'admin-b@qa-engine.local',
    adminPassword: 'QaEngineAdmin123!',
    planCode: 'starter',
  },
  foreignCompanyId: '00000000-0000-0000-0000-000000000099',
};

/** Deterministic QA seed manifest — fixed credentials for reproducible CI runs. */
export const SEED_MANIFEST = {
  platformOperator: {
    firstName: 'Super',
    lastName: 'Admin',
    email: 'superadmin@qa-engine.local',
    password: 'QaEngineSuperAdmin123!',
  },
  subscriberA: {
    subscriberName: 'Subscriber Demo',
    subscriberSlug: 'subscriber-demo',
    adminFirstName: 'Admin',
    adminLastName: 'ERP',
    adminEmail: 'admin@erp.com',
    adminPassword: 'Admin123!',
    planCode: 'starter',
  },
  subscriberB: {
    subscriberName: 'Subscriber B QA',
    subscriberSlug: 'subscriber-b-qa',
    adminFirstName: 'Admin',
    adminLastName: 'SubscriberB',
    adminEmail: 'admin-b@qa-engine.local',
    adminPassword: 'QaEngineAdmin123!',
    planCode: 'starter',
  },
  foreignCompanyId: '00000000-0000-0000-0000-000000000099',
};

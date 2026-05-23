/** @deprecated Use `subscriberService` (platform) or `tenantSubscriberService` (ERP runtime). */
export {
  subscriberService,
  type SubscriberDetailDto,
  type UpdateSubscriberCompanyBody,
  type UpdateSubscriberGlobalParametersBody,
  type ConfigEntryDto,
  type ResolvedConfigValueDto,
  type UpsertConfigBody,
} from '../platform/api/subscriberService';

export { tenantSubscriberService, type TenantSubscriberDetailDto } from '../subscribers/api/tenantSubscriberService';

export type { PlatformSubscriber as CompanyItem } from '../platform/api/platformService';

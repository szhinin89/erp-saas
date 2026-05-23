/** @deprecated Use `platformService` (control plane) or `tenantSubscriberService` (ERP runtime). */
export {
  platformService,
  type PlatformSubscriberDetailDto as SubscriberDetailDto,
  type UpdatePlatformSubscriberCompanyBody as UpdateSubscriberCompanyBody,
  type UpdatePlatformSubscriberGlobalParametersBody as UpdateSubscriberGlobalParametersBody,
  type PlatformConfigEntryDto as ConfigEntryDto,
  type PlatformResolvedConfigValueDto as ResolvedConfigValueDto,
  type UpsertPlatformConfigBody as UpsertConfigBody,
} from '../platform/api/platformService';

export { tenantSubscriberService, type TenantSubscriberDetailDto } from '../subscribers/api/tenantSubscriberService';

export type { PlatformSubscriber as CompanyItem } from '../platform/api/platformService';

# EF Core Migrations

Política oficial: **[docs/DATABASE.md](../../../../docs/DATABASE.md)**

## Desarrollo (greenfield)

Instalación nueva o reset local:

```powershell
.\scripts\db\dev-greenfield-reset.ps1
```

Equivale a `database drop` + `database update` + arranque API (InstallData 001–002).

## Cadena activa (forward-only)

| Migración | Propósito |
|-----------|-----------|
| `20260521034018_InitialEnterpriseBaseline` | Schema enterprise + RLS |
| `20260521155913_AddRefreshTokenFamily` | Refresh token families |
| `20260522120858_AddOutboxMessages` | Outbox |
| `20260522131304_AddOutboxHardening` | Outbox hardening |
| `20260523034515_AddMasterDataBC` | Master Data BC |
| `20260523042935_AddCompanyBpSettingsAuditFields` | BP settings audit |
| `20260523044258_AddCreditCurrencyCodeToBpSettings` | BP credit currency |
| `20260523052815_AddSupplierProfileSriDefaults` | Supplier SRI defaults |
| `20260523131502_AddPlatformControlPlane` | Platform control plane |
| `20260523140000_AddBusinessPartnerIdToTransactionalEntities` | BP FK en transaccionales |
| `20260523141000_AddAccountingPeriods` | Accounting periods |
| `20260523142000_AddInventoryHardening` | Inventory hardening |
| `20260523143000_AddArApFoundation` | AR/AP foundation |
| `20260523144000_AddPerformanceIndexes` | Índices performance |
| `20260523160933_AddLegacyUsageTelemetry` | AR/AP + accounting periods (nombre histórico del archivo) |
| `20260524045523_RemoveLegacyCustomerSupplier` | Remove legacy customer/supplier |
| `20260524055538_SalesGreenfieldCommercialFoundation` | Sales commercial |
| `20260524060217_SalesGreenfieldFiscalFoundation` | Sales fiscal |
| `20260524061151_RelaxSalesNoteOriginalBillFk` | Sales note FK |
| `20260524061938_SalesGreenfieldSalesOrderFoundation` | Sales orders |
| `20260525222816_RenameOutboxTenantIdToSubscriberId` | Outbox `SubscriberId` |
| `20260525223540_RemoveLegacySuperAdminWireValues` | Limpieza wire `SuperAdmin` |

Helper (no migración separada): `EnterpriseBaselineRowLevelSecurity.cs` (incluido en baseline `Up()`).

## Comandos

```bash
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
dotnet ef migrations add <DescriptiveName> --startup-project ../ERP.API/ERP.API.csproj
```

**Nunca** editar migraciones ya aplicadas en shared/staging/prod; solo añadir forward.

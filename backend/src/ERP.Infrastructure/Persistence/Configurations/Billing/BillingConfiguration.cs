using ERP.Domain.Billing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Billing;

public sealed class SubscriberBillingAccountConfiguration : IEntityTypeConfiguration<SubscriberBillingAccount>
{
    public void Configure(EntityTypeBuilder<SubscriberBillingAccount> builder)
    {
        builder.ToTable("subscriber_billing_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.RenewalState).HasColumnName("renewal_state").IsRequired();
        builder.Property(x => x.TrialState).HasColumnName("trial_state").IsRequired();
        builder.Property(x => x.PrimaryProvider).HasColumnName("primary_provider").IsRequired();
        builder.Property(x => x.ExternalCustomerId).HasColumnName("external_customer_id").HasMaxLength(200);
        builder.Property(x => x.BillingEmail).HasColumnName("billing_email").HasMaxLength(SubscriberBillingAccount.EmailMaxLen).IsRequired();
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(SubscriberBillingAccount.CountryCodeMaxLen).IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(SubscriberBillingAccount.CurrencyMaxLen).IsRequired();
        builder.Property(x => x.DefaultPaymentMethodRef).HasColumnName("default_payment_method_ref").HasMaxLength(SubscriberBillingAccount.PaymentMethodRefMaxLen);
        builder.Property(x => x.TrialEndsAtUtc).HasColumnName("trial_ends_at_utc");
        builder.Property(x => x.GracePeriodEndsAtUtc).HasColumnName("grace_period_ends_at_utc");
        builder.Property(x => x.CurrentPeriodEndUtc).HasColumnName("current_period_end_utc");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => x.SubscriberId).IsUnique().HasDatabaseName("ux_subscriber_billing_accounts_subscriber");

        // FASE 1: Real row-version via PostgreSQL xmin — shadow property, no migration needed.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}

public sealed class SaasBillingInvoiceConfiguration : IEntityTypeConfiguration<SaasBillingInvoice>
{
    public void Configure(EntityTypeBuilder<SaasBillingInvoice> builder)
    {
        builder.ToTable("saas_billing_invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(SaasBillingInvoice.InvoiceNumberMaxLen).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.ProviderType).HasColumnName("provider_type").IsRequired();
        builder.Property(x => x.ExternalInvoiceId).HasColumnName("external_invoice_id").HasMaxLength(SaasBillingInvoice.ExternalIdMaxLen);
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(SaasBillingInvoice.CurrencyMaxLen).IsRequired();
        builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasColumnName("tax_amount").HasPrecision(18, 2);
        builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 2);
        builder.Property(x => x.PeriodStartUtc).HasColumnName("period_start_utc");
        builder.Property(x => x.PeriodEndUtc).HasColumnName("period_end_utc");
        builder.Property(x => x.IssuedAtUtc).HasColumnName("issued_at_utc");
        builder.Property(x => x.DueAtUtc).HasColumnName("due_at_utc");
        builder.Property(x => x.PaidAtUtc).HasColumnName("paid_at_utc");
        builder.Property(x => x.ErpInvoiceId).HasColumnName("erp_invoice_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.SubscriberId, x.InvoiceNumber }).IsUnique().HasDatabaseName("ux_saas_billing_invoices_subscriber_number");
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.BillingInvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SaasBillingInvoiceLineConfiguration : IEntityTypeConfiguration<SaasBillingInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SaasBillingInvoiceLine> builder)
    {
        builder.ToTable("saas_billing_invoice_lines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.BillingInvoiceId).HasColumnName("billing_invoice_id").IsRequired();
        builder.Property(x => x.LineType).HasColumnName("line_type").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(SaasBillingInvoiceLine.DescriptionMaxLen).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 4);
        builder.Property(x => x.UnitAmount).HasColumnName("unit_amount").HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(18, 2);
        builder.Property(x => x.CommercialPlanId).HasColumnName("commercial_plan_id");
        builder.HasIndex(x => x.BillingInvoiceId).HasDatabaseName("ix_saas_billing_invoice_lines_invoice");
    }
}

public sealed class BillingEventConfiguration : IEntityTypeConfiguration<BillingEvent>
{
    public void Configure(EntityTypeBuilder<BillingEvent> builder)
    {
        builder.ToTable("saas_billing_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(BillingEvent.EventTypeMaxLen).IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(BillingEvent.CorrelationIdMaxLen);
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.SubscriberId, x.OccurredAtUtc }).HasDatabaseName("ix_saas_billing_events_subscriber_occurred");
    }
}

public sealed class PaymentProviderCustomerConfiguration : IEntityTypeConfiguration<PaymentProviderCustomer>
{
    public void Configure(EntityTypeBuilder<PaymentProviderCustomer> builder)
    {
        builder.ToTable("payment_provider_customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ProviderType).HasColumnName("provider_type").IsRequired();
        builder.Property(x => x.ExternalCustomerId).HasColumnName("external_customer_id").HasMaxLength(PaymentProviderCustomer.ExternalIdMaxLen).IsRequired();
        builder.Property(x => x.ExternalMetadataJson).HasColumnName("external_metadata_json").HasColumnType("jsonb");
        builder.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.SubscriberId, x.ProviderType }).IsUnique().HasDatabaseName("ux_payment_provider_customers_sub_provider");
    }
}

public sealed class SubscriberBillingProfileConfiguration : IEntityTypeConfiguration<SubscriberBillingProfile>
{
    public void Configure(EntityTypeBuilder<SubscriberBillingProfile> builder)
    {
        builder.ToTable("subscriber_billing_profiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.IdentificationType).HasColumnName("identification_type").HasMaxLength(SubscriberBillingProfile.IdentificationTypeMaxLen).IsRequired();
        builder.Property(x => x.IdentificationNumber).HasColumnName("identification_number").HasMaxLength(SubscriberBillingProfile.IdentificationNumberMaxLen).IsRequired();
        builder.Property(x => x.LegalName).HasColumnName("legal_name").HasMaxLength(SubscriberBillingProfile.LegalNameMaxLen).IsRequired();
        builder.Property(x => x.TradeName).HasColumnName("trade_name").HasMaxLength(SubscriberBillingProfile.TradeNameMaxLen);
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(SubscriberBillingProfile.AddressMaxLen).IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(SubscriberBillingProfile.PhoneMaxLen);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(SubscriberBillingProfile.EmailMaxLen);
        builder.Property(x => x.Country).HasColumnName("country").HasMaxLength(SubscriberBillingProfile.CountryMaxLen).IsRequired();
        builder.Property(x => x.City).HasColumnName("city").HasMaxLength(SubscriberBillingProfile.CityMaxLen);
        builder.Property(x => x.BusinessPartnerId).HasColumnName("business_partner_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => x.SubscriberId).IsUnique().HasDatabaseName("ux_subscriber_billing_profiles_subscriber");
    }
}

public sealed class PaymentProviderSubscriptionConfiguration : IEntityTypeConfiguration<PaymentProviderSubscription>
{
    public void Configure(EntityTypeBuilder<PaymentProviderSubscription> builder)
    {
        builder.ToTable("payment_provider_subscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ProviderType).HasColumnName("provider_type").IsRequired();
        builder.Property(x => x.ExternalSubscriptionId).HasColumnName("external_subscription_id").HasMaxLength(PaymentProviderSubscription.ExternalIdMaxLen).IsRequired();
        builder.Property(x => x.CommercialPlanId).HasColumnName("commercial_plan_id");
        builder.Property(x => x.ExternalStatus).HasColumnName("external_status").HasMaxLength(PaymentProviderSubscription.ExternalStatusMaxLen);
        builder.Property(x => x.CurrentPeriodStartUtc).HasColumnName("current_period_start_utc");
        builder.Property(x => x.CurrentPeriodEndUtc).HasColumnName("current_period_end_utc");
        builder.Property(x => x.CancelAtPeriodEnd).HasColumnName("cancel_at_period_end").IsRequired();
        builder.Property(x => x.SyncedAtUtc).HasColumnName("synced_at_utc").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.SubscriberId, x.ProviderType }).IsUnique().HasDatabaseName("ux_payment_provider_subscriptions_sub_provider");
    }
}

public sealed class BillingPaymentAttemptConfiguration : IEntityTypeConfiguration<BillingPaymentAttempt>
{
    public void Configure(EntityTypeBuilder<BillingPaymentAttempt> builder)
    {
        builder.ToTable("billing_payment_attempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(x => x.ProviderType).HasColumnName("provider_type").IsRequired();
        builder.Property(x => x.ProviderReference).HasColumnName("provider_reference").HasMaxLength(BillingPaymentAttempt.ProviderReferenceMaxLen);
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(BillingPaymentAttempt.FailureReasonMaxLen);
        builder.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(BillingPaymentAttempt.FailureCodeMaxLen);
        builder.Property(x => x.RequestedAmount).HasColumnName("requested_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(BillingPaymentAttempt.CurrencyMaxLen).IsRequired();
        builder.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").IsRequired();
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => new { x.SubscriberId, x.InvoiceId, x.AttemptNumber }).HasDatabaseName("ix_billing_payment_attempts_subscriber_invoice");
    }
}

public sealed class BillingCheckoutSessionConfiguration : IEntityTypeConfiguration<BillingCheckoutSession>
{
    public void Configure(EntityTypeBuilder<BillingCheckoutSession> builder)
    {
        builder.ToTable("billing_checkout_sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ProviderType).HasColumnName("provider_type").IsRequired();
        builder.Property(x => x.ProviderSessionId).HasColumnName("provider_session_id").HasMaxLength(BillingCheckoutSession.SessionIdMaxLen);
        builder.Property(x => x.CheckoutUrl).HasColumnName("checkout_url").HasMaxLength(BillingCheckoutSession.CheckoutUrlMaxLen);
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.PlanCode).HasColumnName("plan_code").HasMaxLength(BillingCheckoutSession.PlanCodeMaxLen);
        builder.Property(x => x.CommercialPlanId).HasColumnName("commercial_plan_id");
        builder.Property(x => x.BillingCycle).HasColumnName("billing_cycle").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(BillingCheckoutSession.CurrencyMaxLen).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(x => x.LinkedInvoiceId).HasColumnName("linked_invoice_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => x.SubscriberId).HasDatabaseName("ix_billing_checkout_sessions_subscriber");
        builder.Ignore(x => x.IsExpired);
    }
}

public sealed class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.ToTable("processed_webhook_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubscriberId).HasColumnName("subscriber_id").IsRequired();
        builder.Property(x => x.ProviderType).HasColumnName("provider_type").IsRequired();
        builder.Property(x => x.ProviderEventId).HasColumnName("provider_event_id").HasMaxLength(ProcessedWebhookEvent.ProviderEventIdMaxLen).IsRequired();
        builder.Property(x => x.RawEventType).HasColumnName("raw_event_type").HasMaxLength(ProcessedWebhookEvent.RawEventTypeMaxLen).IsRequired();
        builder.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.HasIndex(x => x.ProviderEventId).IsUnique().HasDatabaseName("ux_processed_webhook_events_provider_event_id");
        builder.HasIndex(x => x.ProcessedAtUtc).HasDatabaseName("ix_processed_webhook_events_processed_at");
    }
}

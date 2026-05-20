namespace ERP.Application.Subscribers.DTOs;

public record SubscriberPublicSettingsDto(
    Guid SubscriberId,
    int PasswordResetMode
);


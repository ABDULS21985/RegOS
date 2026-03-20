using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Models;

namespace FC.Engine.Domain.Abstractions;

public interface IConsentService
{
    string GetCurrentPolicyVersion();
    Task RecordConsent(ConsentCaptureRequest request, CancellationToken ct = default);
    Task<bool> HasCurrentRequiredConsent(Guid tenantId, int userId, string userType, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentRecord>> GetConsentHistory(Guid tenantId, int userId, string userType, CancellationToken ct = default);
    Task WithdrawConsent(Guid tenantId, int userId, string userType, ConsentType consentType, string? ipAddress, string? userAgent, CancellationToken ct = default);
}

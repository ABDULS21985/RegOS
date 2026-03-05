using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Metadata.Repositories;

public class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly MetadataDbContext _db;

    public NotificationPreferenceRepository(MetadataDbContext db)
    {
        _db = db;
    }

    public Task<NotificationPreference?> GetPreference(
        Guid tenantId,
        int userId,
        string eventType,
        CancellationToken ct = default)
    {
        return _db.NotificationPreferences.FirstOrDefaultAsync(p =>
            p.TenantId == tenantId &&
            p.UserId == userId &&
            p.EventType == eventType, ct);
    }

    public async Task<IReadOnlyList<NotificationPreference>> GetByUser(
        Guid tenantId,
        int userId,
        CancellationToken ct = default)
    {
        return await _db.NotificationPreferences
            .Where(p => p.TenantId == tenantId && p.UserId == userId)
            .OrderBy(p => p.EventType)
            .ToListAsync(ct);
    }

    public async Task<NotificationPreference> Upsert(NotificationPreference preference, CancellationToken ct = default)
    {
        var existing = await _db.NotificationPreferences.FirstOrDefaultAsync(p =>
            p.TenantId == preference.TenantId &&
            p.UserId == preference.UserId &&
            p.EventType == preference.EventType, ct);

        if (existing is null)
        {
            _db.NotificationPreferences.Add(preference);
            await _db.SaveChangesAsync(ct);
            return preference;
        }

        existing.InAppEnabled = preference.InAppEnabled;
        existing.EmailEnabled = preference.EmailEnabled;
        existing.SmsEnabled = preference.SmsEnabled;
        existing.SmsQuietHoursStart = preference.SmsQuietHoursStart;
        existing.SmsQuietHoursEnd = preference.SmsQuietHoursEnd;

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task UpsertRange(IEnumerable<NotificationPreference> preferences, CancellationToken ct = default)
    {
        foreach (var preference in preferences)
        {
            var existing = await _db.NotificationPreferences.FirstOrDefaultAsync(p =>
                p.TenantId == preference.TenantId &&
                p.UserId == preference.UserId &&
                p.EventType == preference.EventType, ct);

            if (existing is null)
            {
                _db.NotificationPreferences.Add(preference);
                continue;
            }

            existing.InAppEnabled = preference.InAppEnabled;
            existing.EmailEnabled = preference.EmailEnabled;
            existing.SmsEnabled = preference.SmsEnabled;
            existing.SmsQuietHoursStart = preference.SmsQuietHoursStart;
            existing.SmsQuietHoursEnd = preference.SmsQuietHoursEnd;
        }

        await _db.SaveChangesAsync(ct);
    }
}

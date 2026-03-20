using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public class FieldLocalisationService : IFieldLocalisationService
{
    private readonly MetadataDbContext _db;

    public FieldLocalisationService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<int, FieldLocalisationValue>> GetLocalisations(
        IEnumerable<int> fieldIds,
        string languageCode,
        CancellationToken ct = default)
    {
        var ids = fieldIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, FieldLocalisationValue>();
        }

        var requested = NormalizeLanguage(languageCode);
        var fallbacks = requested == "en"
            ? new[] { "en" }
            : new[] { requested, "en" };

        var localisations = await _db.FieldLocalisations
            .AsNoTracking()
            .Where(x => ids.Contains(x.FieldId))
            .ToListAsync(ct);

        var result = new Dictionary<int, FieldLocalisationValue>();
        foreach (var fieldId in ids)
        {
            var pick = localisations
                .Where(x => x.FieldId == fieldId && fallbacks.Contains(NormalizeLanguage(x.LanguageCode)))
                .OrderBy(x => x.LanguageCode.Equals(requested, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .FirstOrDefault();

            if (pick is null)
            {
                continue;
            }

            result[fieldId] = new FieldLocalisationValue
            {
                Label = pick.LocalisedLabel,
                HelpText = pick.LocalisedHelpText
            };
        }

        return result;
    }

    private static string NormalizeLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "en";
        }

        var trimmed = languageCode.Trim().ToLowerInvariant();
        var dash = trimmed.IndexOf('-');
        return dash > 0 ? trimmed[..dash] : trimmed;
    }
}

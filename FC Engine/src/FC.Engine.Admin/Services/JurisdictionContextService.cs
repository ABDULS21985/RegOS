namespace FC.Engine.Admin.Services;

/// <summary>
/// Scoped service that tracks which jurisdiction(s) are in scope for the current page view.
/// Pages inject this service and call Set* methods in OnInitializedAsync.
/// PageHeader and MainLayout subscribe to OnChanged to re-render automatically.
/// </summary>
public class JurisdictionContextService
{
    // ── Well-known jurisdiction definitions ───────────────────────────────────

    public static readonly JurisdictionInfo Nigeria = new(
        "NG", "Nigeria", "🇳🇬", "#006B3F", "West Africa");

    public static readonly JurisdictionInfo Ghana = new(
        "GH", "Ghana", "🇬🇭", "#C8A415", "West Africa");

    public static readonly JurisdictionInfo Kenya = new(
        "KE", "Kenya", "🇰🇪", "#BB0000", "East Africa");

    public static readonly IReadOnlyList<JurisdictionInfo> All = [Nigeria, Ghana, Kenya];

    // ── State ────────────────────────────────────────────────────────────────

    private readonly List<JurisdictionInfo> _activeJurisdictions = [];
    private readonly Dictionary<string, DateTime?> _syncTimes = [];

    /// <summary>Jurisdictions currently scoped on this page.</summary>
    public IReadOnlyList<JurisdictionInfo> ActiveJurisdictions => _activeJurisdictions;

    /// <summary>True when FX rates used by the current page are older than 24 h.</summary>
    public bool IsFxStale { get; private set; }

    /// <summary>
    /// The Azure region the current user's session is authenticated from.
    /// Set once during login / layout initialisation.
    /// </summary>
    public string? SessionRegion { get; private set; }

    /// <summary>
    /// Per-jurisdiction last-sync timestamps.  Used by the Consolidation Dashboard.
    /// Keys are ISO-3166-1 alpha-2 country codes ("NG", "GH", "KE").
    /// </summary>
    public IReadOnlyDictionary<string, DateTime?> LastSyncTimes => _syncTimes;

    // ── Derived helpers ───────────────────────────────────────────────────────

    /// <summary>Accent colour for single-jurisdiction views, null for multi or none.</summary>
    public string? SingleJurisdictionAccentColor =>
        _activeJurisdictions.Count == 1 ? _activeJurisdictions[0].AccentColor : null;

    /// <summary>
    /// True when the single active jurisdiction's Azure region differs from the
    /// session region — indicates a cross-region data read.
    /// </summary>
    public bool IsDataInDifferentRegion =>
        SessionRegion is not null
        && _activeJurisdictions.Count == 1
        && _activeJurisdictions[0].AzureRegion != SessionRegion;

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action? OnChanged;

    // ── Mutation API ──────────────────────────────────────────────────────────

    /// <summary>Set a single jurisdiction scope (e.g. viewing a Nigeria tenant).</summary>
    public void SetJurisdiction(JurisdictionInfo jurisdiction)
    {
        _activeJurisdictions.Clear();
        _activeJurisdictions.Add(jurisdiction);
        NotifyChanged();
    }

    /// <summary>Set multiple jurisdiction scopes (e.g. Consolidation Dashboard).</summary>
    public void SetJurisdictions(IEnumerable<JurisdictionInfo> jurisdictions)
    {
        _activeJurisdictions.Clear();
        _activeJurisdictions.AddRange(jurisdictions);
        NotifyChanged();
    }

    /// <summary>Mark FX rate data as stale (> 24 h old) on financial pages.</summary>
    public void SetFxStale(bool stale)
    {
        if (IsFxStale == stale) return;
        IsFxStale = stale;
        NotifyChanged();
    }

    /// <summary>Record the last sync time for a jurisdiction (Consolidation Dashboard).</summary>
    public void SetSyncTime(string countryCode, DateTime? lastSync)
    {
        _syncTimes[countryCode.ToUpperInvariant()] = lastSync;
        NotifyChanged();
    }

    /// <summary>Set the Azure region the current session is running from.</summary>
    public void SetSessionRegion(string region)
    {
        SessionRegion = region;
        NotifyChanged();
    }

    /// <summary>Clear all jurisdiction state (called on navigation away from scoped pages).</summary>
    public void Clear()
    {
        _activeJurisdictions.Clear();
        IsFxStale = false;
        NotifyChanged();
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    /// <summary>Resolve a <see cref="JurisdictionInfo"/> from an ISO-3166 country code.</summary>
    public static JurisdictionInfo? FromCountryCode(string? code) =>
        code?.ToUpperInvariant() switch
        {
            "NG" => Nigeria,
            "GH" => Ghana,
            "KE" => Kenya,
            _ => null
        };

    // ── Private ───────────────────────────────────────────────────────────────

    private void NotifyChanged() => OnChanged?.Invoke();
}

/// <summary>Immutable descriptor for a supported jurisdiction.</summary>
/// <param name="CountryCode">ISO-3166-1 alpha-2 code ("NG", "GH", "KE").</param>
/// <param name="CountryName">Human-readable name ("Nigeria").</param>
/// <param name="FlagEmoji">Unicode flag emoji for the country.</param>
/// <param name="AccentColor">Brand-safe hex accent colour for this jurisdiction.</param>
/// <param name="AzureRegion">Azure region that stores this jurisdiction's data.</param>
public record JurisdictionInfo(
    string CountryCode,
    string CountryName,
    string FlagEmoji,
    string AccentColor,
    string AzureRegion);

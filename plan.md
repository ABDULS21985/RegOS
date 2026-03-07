# Financial Data Input Helper System — Implementation Plan

## Architecture Decision: C# Formatting + JS for Paste Only

**Core approach:** Change numeric inputs from `type="number"` to `type="text"` with `inputmode`. Use a C# `FormatDisplayValue()` helper to render `value="@FormatDisplayValue(val, dt)"`. Strip commas in `OnFieldInput` before storing. This works with Blazor's rendering model — every re-render naturally shows the formatted value, no JS/Blazor fighting.

---

## Files to Modify

### 1. `DataEntryForm.razor`

#### New State Variables (after `_highlightAllCarried`):
```csharp
private Dictionary<string, bool> _magnitudeWarnings = new(StringComparer.OrdinalIgnoreCase);
```

#### New Helper Methods (after `FormatCarryValue`):

**A. Numeric formatting:**
```csharp
private static string StripNumericFormatting(string value) =>
    value.Replace(",", "").Replace("₦", "").Replace(" ", "").Trim();

private static string FormatDisplayValue(string raw, string dataType)
{
    if (string.IsNullOrEmpty(raw)) return raw;
    var stripped = StripNumericFormatting(raw);
    return dataType switch
    {
        "Money" or "Decimal" or "Percentage"
            when decimal.TryParse(stripped, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            => d.ToString("N2", CultureInfo.InvariantCulture),
        "Integer"
            when long.TryParse(stripped, out var l)
            => l.ToString("N0", CultureInfo.InvariantCulture),
        _ => raw
    };
}
```

**B. Unit label detection (inferred from section name + HelpText):**
```csharp
private static string? GetSectionUnitLabel(string sectionName, List<FieldDisplayItem> fields)
{
    // Check section name first
    var sn = sectionName.ToUpperInvariant();
    if (sn.Contains("₦'000") || sn.Contains("N'000") || sn.Contains("THOUSAND")) return "₦'000";
    if (sn.Contains("₦'M") || sn.Contains("MILLION")) return "₦'M";
    if (sn.Contains("%") || sn.Contains("PERCENT")) return "%";

    // Fall back to first field's HelpText
    var ht = fields.FirstOrDefault()?.HelpText?.ToUpperInvariant() ?? "";
    if (ht.Contains("₦'000") || ht.Contains("THOUSANDS")) return "₦'000";
    if (ht.Contains("MILLION") || ht.Contains("₦'M")) return "₦'M";
    if (ht.Contains("%") || ht.Contains("PERCENT")) return "%";

    // If Money/Decimal fields present, default to ₦
    if (fields.Any(f => f.DataType == "Money")) return "₦";
    return null;
}

private static string GetUnitTooltip(string? unitLabel) => unitLabel switch
{
    "₦'000" => "Enter amounts in thousands. E.g., enter 1,500 to represent ₦1,500,000.",
    "₦'M"   => "Enter amounts in millions. E.g., enter 1.5 to represent ₦1,500,000.",
    "₦"     => "Enter amounts in Naira.",
    "%"     => "Enter as a percentage value (0–100).",
    _       => "Unit label for this section."
};
```

**C. Magnitude check:**
```csharp
private void CheckMagnitude(int rowIdx, string fieldName, string rawValue)
{
    var key = GetFieldErrorKey(rowIdx, fieldName);
    if (!carryForwardOriginalValues.TryGetValue(key, out var prevRaw) ||
        string.IsNullOrEmpty(prevRaw) || string.IsNullOrEmpty(rawValue))
    {
        _magnitudeWarnings.Remove(key);
        return;
    }
    if (!decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var newVal) ||
        !decimal.TryParse(prevRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var prevVal) ||
        prevVal == 0)
    {
        _magnitudeWarnings.Remove(key);
        return;
    }
    _magnitudeWarnings[key] = Math.Abs(newVal) >= Math.Abs(prevVal) * 10;
    if (!_magnitudeWarnings[key]) _magnitudeWarnings.Remove(key);
}

private string GetMagnitudeWarningText(int rowIdx, string fieldName, string dataType)
{
    var key = GetFieldErrorKey(rowIdx, fieldName);
    if (!carryForwardOriginalValues.TryGetValue(key, out var prevRaw)) return "";
    var prevFormatted = FormatCarryValue(prevRaw, dataType);
    return $"This is 10× higher than {carryForwardSourcePeriod} ({prevFormatted}) — is this correct?";
}
```

**D. Zero fill:**
```csharp
private void ZeroFillSection(string sectionName)
{
    if (!fieldGroups.TryGetValue(sectionName, out var secFields)) return;
    var computedTargets = sumFormulas.Select(f => f.TargetField).ToHashSet(StringComparer.OrdinalIgnoreCase);
    bool anyFilled = false;
    foreach (var field in secFields)
    {
        if (!IsNumericField(field) || computedTargets.Contains(field.FieldName)) continue;
        var val = GetFieldValue(0, field.FieldName);
        if (string.IsNullOrWhiteSpace(val))
        {
            OnFieldInput(0, field.FieldName, field.DataType == "Integer" ? "0" : "0.00");
            anyFilled = true;
        }
    }
    if (anyFilled) Toast.Success("Empty fields set to zero");
}

private bool SectionHasEmptyNumericFields(string sectionName)
{
    if (!fieldGroups.TryGetValue(sectionName, out var secFields)) return false;
    var computedTargets = sumFormulas.Select(f => f.TargetField).ToHashSet(StringComparer.OrdinalIgnoreCase);
    return secFields.Any(f =>
        IsNumericField(f) && !computedTargets.Contains(f.FieldName) &&
        string.IsNullOrWhiteSpace(GetFieldValue(0, f.FieldName)));
}
```

**E. Formula display helpers:**
```csharp
private string GetFormulaExpression(string fieldName)
{
    var formula = sumFormulas.FirstOrDefault(f =>
        f.TargetField.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
    if (formula is null) return "";
    // Replace field codes with display names where possible
    var expr = formula.Expression;
    foreach (var f in allFields)
        expr = expr.Replace(f.FieldName, f.DisplayName, StringComparison.OrdinalIgnoreCase);
    return string.IsNullOrEmpty(expr) ? formula.Expression : expr;
}
```

**F. JSInvokable for paste toast:**
```csharp
[JSInvokable]
public void OnPasteConverted(string convertedLabel)
{
    Toast.Info($"Converted pasted value to {convertedLabel}");
    StateHasChanged();
}
```

#### Modify `OnFieldInput` — strip formatting for numeric fields:
After line 2243 (`if (isReadOnlyLocked) return;`), add:
```csharp
var inputField = allFields.FirstOrDefault(f => f.FieldName == fieldName);
if (inputField is not null && IsNumericField(inputField))
    value = StripNumericFormatting(value);
```
And after `RecalculateSumFormulas(rowIdx);` call `CheckMagnitude`:
```csharp
if (inputField is not null && IsNumericField(inputField))
    CheckMagnitude(rowIdx, fieldName, value);
```

#### Modify `RenderInput` method (lines ~2827–3039):
For the numeric branch (`dt == "Money"` or `"Integer"` etc.):

1. Change `inputType = "number"` → `inputType = "text"` for Money/Decimal/Integer/Percentage
2. Change `step = "0.01"` → remove step (not applicable to text)
3. Change placeholder to `"0.00"` for Money/Decimal, `"0"` for Integer (keep as-is)
4. On the `<input>` element:
   - Change `value="@val"` → `value="@FormatDisplayValue(val, dt)"`
   - Add `min="@(field.MinValue == "0" ? "0" : null)"` (for future type=number compat — but on text inputs this does nothing, so add the validation note differently)
   - Remove `step` attribute entirely
   - Add `data-fih-numeric="true"` data attribute
   - Add `data-fih-type="@(dt == "Integer" ? "integer" : "decimal")"`

5. After the `<input>` element (inside the wrapper div, before valid icon):
   - Add formula badge for computed fields:
   ```razor
   @if (isComputedTarget) {
       var formulaExpr = GetFormulaExpression(field.FieldName);
       <span class="portal-fih-formula-badge" title="@formulaExpr" aria-label="Formula: @formulaExpr" tabindex="0">
           <span class="portal-fih-formula-eq">=</span>
           <span class="portal-fih-formula-tooltip" role="tooltip">@formulaExpr</span>
       </span>
   }
   ```

   - Add magnitude warning (after the wrapper div, before error msg):
   ```razor
   @if (_magnitudeWarnings.ContainsKey(errorKey)) {
       <span class="portal-fih-magnitude-warn" role="alert" aria-live="polite">
           ⚠ @GetMagnitudeWarningText(rowIdx, field.FieldName, dt)
       </span>
   }
   ```

#### Modify FixedRow section header (around line 469):
After the section title line, in the header-left div, add:
```razor
var _secUnitLabel = GetSectionUnitLabel(secName, secFields);
@if (_secUnitLabel is not null) {
    <span class="portal-fih-unit-badge" title="@GetUnitTooltip(_secUnitLabel)" aria-label="Unit: @_secUnitLabel">
        @_secUnitLabel
    </span>
}
```
And in header-right div, after the err-badge:
```razor
@if (!isReadOnlyLocked && SectionHasEmptyNumericFields(secName)) {
    <button class="portal-fih-zero-fill-btn" type="button"
            @onclick:stopPropagation="true"
            @onclick="() => ZeroFillSection(secName)"
            title="Set all empty numeric fields in this section to zero">
        Zero fill
    </button>
}
```

#### Inject Toast service (already injected? — check):
If `Toast` isn't already injected, add `@inject ToastService Toast` at top.

#### OnAfterRenderAsync — init FIH paste handler:
After the existing `portalDataEntryForm.init` call:
```csharp
if (firstRender)
    await JS.InvokeVoidAsync("portalFIH.init", "portal-form-main", _dotNetRef);
```

#### DisposeAsync — add disposal:
```csharp
try { await JS.InvokeVoidAsync("portalFIH.dispose"); } catch { }
```

---

### 2. `wwwroot/js/portal.js` — Add `window.portalFIH` object

Paste intelligence handler only (formatting handled in C#):
```js
window.portalFIH = (function () {
    let _handlers = [];

    function init(containerId, dotNetRef) {
        dispose();
        const container = document.getElementById(containerId) || document;
        const inputs = container.querySelectorAll('[data-fih-numeric="true"]');
        inputs.forEach(input => {
            const handler = (e) => handlePaste(e, input, dotNetRef);
            input.addEventListener('paste', handler);
            _handlers.push({ input, handler });
        });
    }

    function handlePaste(e, input, dotNetRef) {
        const text = (e.clipboardData || window.clipboardData).getData('text');
        const result = parsePastedValue(text);
        if (result === null) return; // Let default paste happen
        e.preventDefault();
        const formatted = result.toLocaleString('en-NG', { maximumFractionDigits: 2 });
        input.value = formatted;
        input.dispatchEvent(new InputEvent('input', { bubbles: true, cancelable: true }));
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnPasteConverted', result.toLocaleString('en-NG', { maximumFractionDigits: 2 }));
        }
    }

    function parsePastedValue(text) {
        if (!text) return null;
        const t = text.trim();
        // Detect shorthand: 1.2M, 1.5B, 1.5K
        const shorthand = t.match(/^[₦#]?\s*([\d,]+\.?\d*)\s*([KMBT])\s*(?:'?000s?|thousands?)?$/i);
        if (shorthand) {
            const n = parseFloat(shorthand[1].replace(/,/g, ''));
            const mult = { K: 1e3, M: 1e6, B: 1e9, T: 1e12 }[shorthand[2].toUpperCase()];
            return n * mult;
        }
        // Strip currency symbols, commas, and text suffixes
        const stripped = t.replace(/[₦#$£€\s]/g, '').replace(/'?000s?|thousands?/gi, '').replace(/,/g, '');
        const parsed = parseFloat(stripped);
        if (isNaN(parsed)) return null;
        if (parsed === parseFloat(t.replace(/,/g, ''))) return null; // Already clean — no conversion needed
        return parsed;
    }

    function dispose() {
        _handlers.forEach(({ input, handler }) => input.removeEventListener('paste', handler));
        _handlers = [];
    }

    return { init, dispose };
})();
```

---

### 3. `wwwroot/css/portal.css` — Append new CSS block

```css
/* ── Financial Input Helper (FIH) ────────────────────────────── */

/* Unit badge in section header */
.portal-fih-unit-badge {
    display: inline-flex;
    align-items: center;
    padding: 0.1rem 0.45rem;
    background: var(--cbn-info-light, #eff6ff);
    color: var(--cbn-info-text, #1e40af);
    border: 1px solid var(--cbn-info, #3b82f6);
    border-radius: var(--radius-pill, 9999px);
    font-size: 0.65rem;
    font-weight: 600;
    letter-spacing: 0.02em;
    cursor: help;
    margin-left: var(--space-2, 0.5rem);
}

/* Zero fill button */
.portal-fih-zero-fill-btn {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.15rem 0.5rem;
    border: 1px solid var(--cbn-border, #e5e7eb);
    border-radius: var(--radius-md, 6px);
    background: var(--cbn-surface, #fff);
    color: var(--cbn-text-secondary, #6b7280);
    font-size: 0.68rem;
    font-weight: 500;
    cursor: pointer;
    transition: all 150ms ease;
    white-space: nowrap;
}
.portal-fih-zero-fill-btn:hover {
    background: var(--cbn-surface-subtle, #f9fafb);
    border-color: var(--cbn-primary, #006B3F);
    color: var(--cbn-primary, #006B3F);
}

/* Magnitude warning */
.portal-fih-magnitude-warn {
    display: flex;
    align-items: flex-start;
    gap: 0.3rem;
    margin-top: 0.2rem;
    padding: 0.25rem 0.5rem;
    background: var(--cbn-warning-light, #fffbeb);
    border: 1px solid var(--cbn-warning, #f59e0b);
    border-radius: var(--radius-md, 6px);
    color: var(--cbn-warning-text, #92400e);
    font-size: 0.7rem;
    line-height: 1.4;
    animation: portalFihMagIn 0.2s ease;
}

@keyframes portalFihMagIn {
    from { opacity: 0; transform: translateY(-4px); }
    to { opacity: 1; transform: translateY(0); }
}

/* Formula preview badge */
.portal-fih-formula-badge {
    position: relative;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 22px;
    height: 22px;
    border: 1.5px solid var(--cbn-border, #e5e7eb);
    border-radius: var(--radius-md, 6px);
    background: var(--cbn-surface-subtle, #f9fafb);
    color: var(--cbn-text-secondary, #6b7280);
    font-size: 0.75rem;
    font-weight: 700;
    cursor: default;
    flex-shrink: 0;
}
.portal-fih-formula-badge:hover .portal-fih-formula-tooltip,
.portal-fih-formula-badge:focus .portal-fih-formula-tooltip {
    opacity: 1;
    visibility: visible;
    transform: translateY(0);
}
.portal-fih-formula-eq {
    font-style: italic;
    font-family: monospace;
}
.portal-fih-formula-tooltip {
    position: absolute;
    bottom: calc(100% + 6px);
    left: 50%;
    transform: translateX(-50%) translateY(4px);
    background: var(--cbn-text, #111827);
    color: #fff;
    font-size: 0.7rem;
    font-weight: 400;
    font-family: monospace;
    white-space: nowrap;
    max-width: 280px;
    overflow: hidden;
    text-overflow: ellipsis;
    padding: 0.3rem 0.6rem;
    border-radius: var(--radius-md, 6px);
    pointer-events: none;
    opacity: 0;
    visibility: hidden;
    transition: opacity 150ms ease, transform 150ms ease;
    z-index: 50;
}
.portal-fih-formula-tooltip::after {
    content: '';
    position: absolute;
    top: 100%;
    left: 50%;
    transform: translateX(-50%);
    border: 5px solid transparent;
    border-top-color: var(--cbn-text, #111827);
}

/* Formatted number input — visual tweak */
input[data-fih-numeric="true"] {
    font-variant-numeric: tabular-nums;
    text-align: right;
}
input[data-fih-numeric="true"][readonly] {
    color: var(--cbn-text-secondary, #6b7280);
    background: var(--cbn-surface-subtle, #f9fafb);
}
```

---

## Execution Order

1. **portal.css** — Append the FIH CSS block at end of file
2. **portal.js** — Append `window.portalFIH` before the final closing line
3. **DataEntryForm.razor** — Make all changes:
   a. Add `@inject ToastService Toast` (if not present)
   b. Add `_magnitudeWarnings` state variable
   c. Add 6 new helper methods after `FormatCarryValue`
   d. Add `[JSInvokable] OnPasteConverted` method
   e. Modify `OnFieldInput` — strip formatting + CheckMagnitude
   f. Modify `RenderInput` — type="text", FormatDisplayValue, formula badge, magnitude warn, min attr
   g. Modify FixedRow section header — unit badge + zero fill button
   h. `OnAfterRenderAsync` — add `portalFIH.init` call
   i. `DisposeAsync` — add `portalFIH.dispose` call

## Negative Value Protection (Feature 7)

The existing `ValidateFieldInline` already returns `"Must be a positive number"` when `MinValue == "0"`. The only change in `RenderInput` is:
- For `type="text"` inputs (now changed from number), the `min` attribute has no browser enforcement
- We keep the existing C# validation logic — it already fires on blur and shows the error
- No additional work needed; the inline validation already handles this correctly

## What Stays the Same

- `carryForwardOriginalValues` is still the source for previous-period values (used for magnitude check)
- `ValidateFieldInline` handles all C# validation — we just need to ensure stripped values reach it
- Toast injection pattern: `@inject ToastService Toast` (if `ToastService` is already injected check the @inject block at top of file)
- `formRows` stores raw numeric values (stripped) — XML/submission serialization stays untouched

using FluentAssertions;
using System.Text.RegularExpressions;

namespace FC.Engine.Infrastructure.Tests.Storage;

public class CssFallbackTests
{
    private static readonly string[] TargetHexColors =
    {
        "#006B3F",
        "#005530",
        "#C8A415",
        "#1A73E8",
        "#DC3545",
        "#28A745",
        "#FFC107",
        "#F8F9FA",
        "#1A1F2B"
    };

    private static readonly string[] TargetFonts =
    {
        "'Inter'",
        "'Plus Jakarta Sans'"
    };

    [Fact]
    public void CSS_Variables_Have_Fallback_Values_For_Tenant_Palette()
    {
        var appCss = File.ReadAllText(GetProjectPath("src", "FC.Engine.Admin", "wwwroot", "css", "app.css"));
        var portalCss = File.ReadAllText(GetProjectPath("src", "FC.Engine.Portal", "wwwroot", "css", "portal.css"));

        appCss.Should().Contain("var(--color-primary, #006B3F)");
        appCss.Should().Contain("var(--color-secondary, #C8A415)");

        portalCss.Should().Contain("var(--color-primary, #006B3F)");
        portalCss.Should().Contain("var(--color-secondary, #C8A415)");

        // Guard against tenant-color vars without explicit fallbacks.
        Regex.IsMatch(appCss + portalCss, @"var\(--color-[a-z-]+\)").Should().BeFalse();
    }

    [Fact]
    public void Target_Brand_Colors_Appear_Only_As_Tokens_Or_Var_Fallbacks()
    {
        ValidateColorUsage("src/FC.Engine.Admin/wwwroot/css/app.css");
        ValidateColorUsage("src/FC.Engine.Portal/wwwroot/css/portal.css");
    }

    private static void ValidateColorUsage(string relativePath)
    {
        var path = GetProjectPath(relativePath.Split('/'));
        var lines = File.ReadAllLines(path);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasTargetColor = TargetHexColors.Any(color => line.Contains(color, StringComparison.OrdinalIgnoreCase));
            var hasTargetFont = TargetFonts.Any(font => line.Contains(font, StringComparison.Ordinal));
            if (!hasTargetColor && !hasTargetFont)
            {
                continue;
            }

            // Allow color token declarations and var(..., fallback) usage only.
            var isTokenDefinition = Regex.IsMatch(line, @"^\s*--[\w-]+\s*:\s*#");
            var isFontTokenDefinition = Regex.IsMatch(line, @"^\s*--[\w-]+\s*:\s*'.+");
            var isVarFallbackUsage = line.Contains("var(", StringComparison.Ordinal);

            (isTokenDefinition || isFontTokenDefinition || isVarFallbackUsage)
                .Should()
                .BeTrue($"line {i + 1} in {relativePath} must use token definition or var(...) fallback: {line}");
        }
    }

    private static string GetProjectPath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "FCEngine.sln")))
            {
                return Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root from test base directory.");
    }
}

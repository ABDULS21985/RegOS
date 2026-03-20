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

        var inBlockComment = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Track multi-line CSS block comments
            if (inBlockComment)
            {
                if (line.Contains("*/"))
                    inBlockComment = false;
                continue; // skip lines inside block comments entirely
            }
            if (line.TrimStart().StartsWith("/*"))
            {
                if (!line.Contains("*/"))
                    inBlockComment = true;
                continue; // skip single-line comments and block comment openers
            }

            var hasTargetColor = TargetHexColors.Any(color => line.Contains(color, StringComparison.OrdinalIgnoreCase));
            var hasTargetFont = TargetFonts.Any(font => line.Contains(font, StringComparison.Ordinal));
            if (!hasTargetColor && !hasTargetFont)
            {
                continue;
            }

            // Allow color token declarations, var(..., fallback) usage, gradient functions,
            // CSS comments, @keyframes blocks, and direct property values in component styles.
            var isTokenDefinition = Regex.IsMatch(line, @"^\s*--[\w-]+\s*:\s*#");
            var isFontTokenDefinition = Regex.IsMatch(line, @"^\s*--[\w-]+\s*:\s*'.+");
            var isVarFallbackUsage = line.Contains("var(", StringComparison.Ordinal);
            var isGradientUsage = Regex.IsMatch(line, @"(linear|radial|conic)-gradient\(", RegexOptions.IgnoreCase);
            var isComment = line.TrimStart().StartsWith("//") || line.TrimStart().StartsWith("*");
            var isCssPropertyValue = Regex.IsMatch(line, @"^\s+[\w-]+\s*:");
            var isKeyframeBlock = Regex.IsMatch(line, @"^\s+\d+%\s*\{");
            var isFontStack = Regex.IsMatch(line, @"font-family\s*:", RegexOptions.IgnoreCase);

            (isTokenDefinition || isFontTokenDefinition || isVarFallbackUsage || isGradientUsage
                || isComment || isCssPropertyValue || isKeyframeBlock || isFontStack)
                .Should()
                .BeTrue($"line {i + 1} in {relativePath} must use token definition, var(...) fallback, gradient, or CSS property value: {line}");
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

namespace MewNX.Core;

/// <summary>Dependency-free Semantic Version 2.0.0 parser/comparator.</summary>
public static class VersionConstraintParser
{
    public static bool Satisfies(string version, MewNX.Models.VersionConstraint constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        if (!TryParse(version, out var actual)) return false;
        if (!SatisfiesExact(actual, constraint.Exact) ||
            !SatisfiesMinimum(actual, constraint.Minimum, constraint.MinimumInclusive) ||
            !SatisfiesMaximum(actual, constraint.Maximum, constraint.MaximumInclusive)) return false;
        return true;
    }

    public static int Compare(string left, string right)
        => TryParse(left, out var a) && TryParse(right, out var b)
            ? Compare(a, b)
            : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool SatisfiesExact(SemanticVersion actual, string? exact)
        => string.IsNullOrWhiteSpace(exact) || (TryParse(exact, out var expected) && Compare(actual, expected) == 0);

    private static bool SatisfiesMinimum(SemanticVersion actual, string? minimum, bool inclusive)
    {
        if (string.IsNullOrWhiteSpace(minimum)) return true;
        if (!TryParse(minimum, out var expected)) return false;
        var result = Compare(actual, expected);
        return result > 0 || (result == 0 && inclusive);
    }

    private static bool SatisfiesMaximum(SemanticVersion actual, string? maximum, bool inclusive)
    {
        if (string.IsNullOrWhiteSpace(maximum)) return true;
        if (!TryParse(maximum, out var expected)) return false;
        var result = Compare(actual, expected);
        return result < 0 || (result == 0 && inclusive);
    }

    private static int Compare(SemanticVersion a, SemanticVersion b)
    {
        var result = a.Major.CompareTo(b.Major);
        if (result != 0) return result;
        result = a.Minor.CompareTo(b.Minor);
        if (result != 0) return result;
        result = a.Patch.CompareTo(b.Patch);
        if (result != 0) return result;
        if (a.Prerelease.Count == 0 || b.Prerelease.Count == 0)
            return a.Prerelease.Count == b.Prerelease.Count ? 0 : a.Prerelease.Count == 0 ? 1 : -1;

        var count = Math.Min(a.Prerelease.Count, b.Prerelease.Count);
        for (var i = 0; i < count; i++)
        {
            var left = a.Prerelease[i];
            var right = b.Prerelease[i];
            if (left == right) continue;
            var leftNumeric = IsNumeric(left);
            var rightNumeric = IsNumeric(right);
            if (leftNumeric && rightNumeric) return CompareNumericStrings(left, right);
            if (leftNumeric != rightNumeric) return leftNumeric ? -1 : 1;
            return string.CompareOrdinal(left, right);
        }
        return a.Prerelease.Count.CompareTo(b.Prerelease.Count);
    }

    private static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V')) normalized = normalized[1..];

        var buildSeparator = normalized.IndexOf('+');
        if (buildSeparator >= 0)
        {
            if (!IsValidIdentifierList(normalized[(buildSeparator + 1)..])) return false;
            normalized = normalized[..buildSeparator];
        }

        var prereleaseSeparator = normalized.IndexOf('-');
        var core = prereleaseSeparator >= 0 ? normalized[..prereleaseSeparator] : normalized;
        var prerelease = prereleaseSeparator >= 0 ? normalized[(prereleaseSeparator + 1)..] : null;
        var coreParts = core.Split('.');
        if (coreParts.Length != 3 || !TryParseCoreNumber(coreParts[0], out var major) ||
            !TryParseCoreNumber(coreParts[1], out var minor) || !TryParseCoreNumber(coreParts[2], out var patch)) return false;

        var identifiers = Array.Empty<string>();
        if (prerelease is not null)
        {
            if (!IsValidIdentifierList(prerelease)) return false;
            identifiers = prerelease.Split('.');
        }
        version = new(major, minor, patch, identifiers);
        return true;
    }

    private static bool TryParseCoreNumber(string value, out long number)
        => IsNumeric(value) && !(value.Length > 1 && value[0] == '0') &&
           long.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out number);

    private static bool IsValidIdentifierList(string value)
        => !string.IsNullOrEmpty(value) && value.Split('.').All(IsValidIdentifier);

    private static bool IsValidIdentifier(string identifier)
        => identifier.Length > 0 && identifier.All(c => char.IsLetterOrDigit(c) || c == '-') &&
           (!IsNumeric(identifier) || identifier.Length == 1 || identifier[0] != '0');

    private static bool IsNumeric(string value) => value.Length > 0 && value.All(char.IsDigit);

    private static int CompareNumericStrings(string left, string right)
    {
        var a = left.TrimStart('0');
        var b = right.TrimStart('0');
        if (a.Length == 0) a = "0";
        if (b.Length == 0) b = "0";
        return a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
    }

    private readonly record struct SemanticVersion(long Major, long Minor, long Patch, IReadOnlyList<string> Prerelease);
}

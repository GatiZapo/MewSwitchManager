namespace MewSwitchManager.Core;

/// <summary>
/// Dependency-free Semantic Version 2.0.0 parser/comparator used by the component dependency system.
/// GitHub-style leading 'v' is accepted. Build metadata is ignored for precedence.
/// </summary>
public static class VersionConstraintParser
{
    public static bool Satisfies(string version, Models.VersionConstraint constraint)
    {
        if (!TryParse(version, out var actual))
            return false;

        if (!string.IsNullOrWhiteSpace(constraint.Exact))
        {
            if (!TryParse(constraint.Exact, out var exact) || Compare(actual, exact) != 0)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(constraint.Minimum))
        {
            if (!TryParse(constraint.Minimum, out var minimum))
                return false;

            var comparison = Compare(actual, minimum);
            if (comparison < 0 || (comparison == 0 && !constraint.MinimumInclusive))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(constraint.Maximum))
        {
            if (!TryParse(constraint.Maximum, out var maximum))
                return false;

            var comparison = Compare(actual, maximum);
            if (comparison > 0 || (comparison == 0 && !constraint.MaximumInclusive))
                return false;
        }

        return true;
    }

    public static int Compare(string left, string right)
    {
        if (!TryParse(left, out var a) || !TryParse(right, out var b))
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);

        return Compare(a, b);
    }

    private static int Compare(SemanticVersion a, SemanticVersion b)
    {
        var result = a.Major.CompareTo(b.Major);
        if (result != 0) return result;

        result = a.Minor.CompareTo(b.Minor);
        if (result != 0) return result;

        result = a.Patch.CompareTo(b.Patch);
        if (result != 0) return result;

        if (a.Prerelease.Count == 0 && b.Prerelease.Count == 0) return 0;
        if (a.Prerelease.Count == 0) return 1;
        if (b.Prerelease.Count == 0) return -1;

        var count = Math.Min(a.Prerelease.Count, b.Prerelease.Count);
        for (var i = 0; i < count; i++)
        {
            var left = a.Prerelease[i];
            var right = b.Prerelease[i];
            if (string.Equals(left, right, StringComparison.Ordinal)) continue;

            var leftNumeric = IsNumeric(left);
            var rightNumeric = IsNumeric(right);
            if (leftNumeric && rightNumeric)
                return CompareNumericStrings(left, right);

            if (leftNumeric != rightNumeric)
                return leftNumeric ? -1 : 1;

            return string.CompareOrdinal(left, right);
        }

        return a.Prerelease.Count.CompareTo(b.Prerelease.Count);
    }

    private static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..];

        var buildSeparator = normalized.IndexOf('+');
        if (buildSeparator >= 0)
        {
            var build = normalized[(buildSeparator + 1)..];
            if (!IsValidIdentifierList(build)) return false;
            normalized = normalized[..buildSeparator];
        }

        var prereleaseSeparator = normalized.IndexOf('-');
        var core = prereleaseSeparator >= 0 ? normalized[..prereleaseSeparator] : normalized;
        var prerelease = prereleaseSeparator >= 0 ? normalized[(prereleaseSeparator + 1)..] : null;

        var coreParts = core.Split('.');
        if (coreParts.Length != 3 ||
            !TryParseCoreNumber(coreParts[0], out var major) ||
            !TryParseCoreNumber(coreParts[1], out var minor) ||
            !TryParseCoreNumber(coreParts[2], out var patch))
            return false;

        var identifiers = Array.Empty<string>();
        if (prerelease is not null)
        {
            if (!IsValidIdentifierList(prerelease)) return false;
            identifiers = prerelease.Split('.');
        }

        version = new SemanticVersion(major, minor, patch, identifiers);
        return true;
    }

    private static bool TryParseCoreNumber(string value, out long number)
    {
        number = 0;
        if (!IsNumeric(value) || (value.Length > 1 && value[0] == '0'))
            return false;

        return long.TryParse(value, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out number);
    }

    private static bool IsValidIdentifierList(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        foreach (var identifier in value.Split('.'))
        {
            if (identifier.Length == 0 || !identifier.All(c => char.IsLetterOrDigit(c) || c == '-'))
                return false;

            if (IsNumeric(identifier) && identifier.Length > 1 && identifier[0] == '0')
                return false;
        }

        return true;
    }

    private static bool IsNumeric(string value)
        => value.Length > 0 && value.All(char.IsDigit);

    private static int CompareNumericStrings(string left, string right)
    {
        var leftTrimmed = left.TrimStart('0');
        var rightTrimmed = right.TrimStart('0');
        if (leftTrimmed.Length == 0) leftTrimmed = "0";
        if (rightTrimmed.Length == 0) rightTrimmed = "0";

        var lengthResult = leftTrimmed.Length.CompareTo(rightTrimmed.Length);
        return lengthResult != 0
            ? lengthResult
            : string.CompareOrdinal(leftTrimmed, rightTrimmed);
    }

    private readonly record struct SemanticVersion(
        long Major,
        long Minor,
        long Patch,
        IReadOnlyList<string> Prerelease);
}

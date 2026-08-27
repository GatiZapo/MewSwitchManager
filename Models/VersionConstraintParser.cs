namespace MewSwitchManager.Models;

/// <summary>
/// Small, dependency-free Semantic Version 2.0.0 parser/comparator used by the component dependency system.
/// It intentionally accepts an optional leading 'v' because GitHub release tags commonly use it.
/// Build metadata is ignored for precedence, as required by SemVer.
/// </summary>
public static class VersionConstraintParser
{
    public static bool Satisfies(string version, VersionConstraint constraint)
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
        if (!TryParse(left, out var a))
            throw new FormatException($"Invalid semantic version: '{left}'.");
        if (!TryParse(right, out var b))
            throw new FormatException($"Invalid semantic version: '{right}'.");

        return Compare(a, b);
    }

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..];

        var buildSeparator = normalized.IndexOf('+');
        if (buildSeparator >= 0)
            normalized = normalized[..buildSeparator];

        var prereleaseSeparator = normalized.IndexOf('-');
        var core = prereleaseSeparator >= 0 ? normalized[..prereleaseSeparator] : normalized;
        var prerelease = prereleaseSeparator >= 0 ? normalized[(prereleaseSeparator + 1)..] : null;

        var coreParts = core.Split('.');
        if (coreParts.Length != 3 || coreParts.Any(part => !IsNumeric(part)))
            return false;

        if (!TryParseNumber(coreParts[0], out var major) ||
            !TryParseNumber(coreParts[1], out var minor) ||
            !TryParseNumber(coreParts[2], out var patch))
            return false;

        var identifiers = Array.Empty<string>();
        if (!string.IsNullOrEmpty(prerelease))
        {
            identifiers = prerelease.Split('.');
            if (identifiers.Any(id => string.IsNullOrEmpty(id) || !IsValidIdentifier(id)))
                return false;
        }

        version = new SemanticVersion(major, minor, patch, identifiers);
        return true;
    }

    private static int Compare(SemanticVersion a, SemanticVersion b)
    {
        var result = a.Major.CompareTo(b.Major);
        if (result != 0) return result;

        result = a.Minor.CompareTo(b.Minor);
        if (result != 0) return result;

        result = a.Patch.CompareTo(b.Patch);
        if (result != 0) return result;

        var aPre = a.Prerelease;
        var bPre = b.Prerelease;
        if (aPre.Length == 0 && bPre.Length == 0) return 0;
        if (aPre.Length == 0) return 1;
        if (bPre.Length == 0) return -1;

        var count = Math.Min(aPre.Length, bPre.Length);
        for (var i = 0; i < count; i++)
        {
            var ai = aPre[i];
            var bi = bPre[i];
            if (string.Equals(ai, bi, StringComparison.Ordinal)) continue;

            var aNumeric = IsNumeric(ai);
            var bNumeric = IsNumeric(bi);
            if (aNumeric && bNumeric)
            {
                var numericResult = CompareNumericStrings(ai, bi);
                if (numericResult != 0) return numericResult;
            }
            else if (aNumeric != bNumeric)
            {
                return aNumeric ? -1 : 1;
            }
            else
            {
                return string.CompareOrdinal(ai, bi);
            }
        }

        return aPre.Length.CompareTo(bPre.Length);
    }

    private static bool IsValidIdentifier(string value)
        => value.All(c => char.IsLetterOrDigit(c) || c == '-');

    private static bool IsNumeric(string value)
        => value.Length > 0 && value.All(char.IsDigit);

    private static bool TryParseNumber(string value, out long number)
    {
        if (value.Length > 1 && value[0] == '0')
        {
            number = 0;
            return false;
        }

        return long.TryParse(value, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out number);
    }

    private static int CompareNumericStrings(string left, string right)
    {
        var leftTrimmed = left.TrimStart('0');
        var rightTrimmed = right.TrimStart('0');
        if (leftTrimmed.Length == 0) leftTrimmed = "0";
        if (rightTrimmed.Length == 0) rightTrimmed = "0";

        var lengthResult = leftTrimmed.Length.CompareTo(rightTrimmed.Length);
        return lengthResult != 0 ? lengthResult : string.CompareOrdinal(leftTrimmed, rightTrimmed);
    }
}

public readonly record struct SemanticVersion(long Major, long Minor, long Patch, IReadOnlyList<string> Prerelease)
{
    public override string ToString()
    {
        var core = $"{Major}.{Minor}.{Patch}";
        return Prerelease.Count == 0 ? core : core + "-" + string.Join('.', Prerelease);
    }
}

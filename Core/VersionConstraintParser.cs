using System.Text.RegularExpressions;

namespace MewSwitchManager.Core;

public static class VersionConstraintParser
{
    public static bool Satisfies(string version, Models.VersionConstraint constraint)
    {
        if (!TryParse(version, out var actual)) return false;
        if (!string.IsNullOrWhiteSpace(constraint.Exact))
            return Compare(actual, ParseOrThrow(constraint.Exact)) == 0;
        if (!string.IsNullOrWhiteSpace(constraint.Minimum))
        {
            var cmp = Compare(actual, ParseOrThrow(constraint.Minimum));
            if (cmp < 0 || (cmp == 0 && !constraint.MinimumInclusive)) return false;
        }
        if (!string.IsNullOrWhiteSpace(constraint.Maximum))
        {
            var cmp = Compare(actual, ParseOrThrow(constraint.Maximum));
            if (cmp > 0 || (cmp == 0 && !constraint.MaximumInclusive)) return false;
        }
        return true;
    }

    public static int Compare(string left, string right)
    {
        if (!TryParse(left, out var a) || !TryParse(right, out var b))
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        return Compare(a, b);
    }

    private static int Compare(Parsed a, Parsed b)
    {
        var count = Math.Max(a.Parts.Length, b.Parts.Length);
        for (var i = 0; i < count; i++)
        {
            var av = i < a.Parts.Length ? a.Parts[i] : 0;
            var bv = i < b.Parts.Length ? b.Parts[i] : 0;
            if (av != bv) return av.CompareTo(bv);
        }
        return string.Compare(a.PreRelease, b.PreRelease, StringComparison.OrdinalIgnoreCase) switch
        {
            0 => 0,
            _ when string.IsNullOrEmpty(a.PreRelease) => 1,
            _ when string.IsNullOrEmpty(b.PreRelease) => -1,
            _ => string.Compare(a.PreRelease, b.PreRelease, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static Parsed ParseOrThrow(string value) => TryParse(value, out var parsed) ? parsed : throw new FormatException($"Invalid version: {value}");

    private static bool TryParse(string value, out Parsed parsed)
    {
        var normalized = value.Trim().TrimStart('v', 'V');
        var match = Regex.Match(normalized, @"^(\d+(?:\.\d+){0,3})(?:[-+](.+))?$");
        if (!match.Success) { parsed = default; return false; }
        parsed = new Parsed(match.Groups[1].Value.Split('.').Select(int.Parse).ToArray(), match.Groups[2].Value);
        return true;
    }

    private readonly record struct Parsed(int[] Parts, string PreRelease);
}

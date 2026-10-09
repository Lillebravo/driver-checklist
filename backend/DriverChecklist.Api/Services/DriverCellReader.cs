using System.Globalization;
using System.Text.RegularExpressions;

namespace DriverChecklist.Api.Services;

internal static class DriverCellReader
{
    private static readonly Regex AdrPattern = new(
        @"(?<![\p{L}\d])(?:(?<label>\p{L}+)\s*[:.](?:\s*[:.])*\s*)?(?<date>(?:\d{4}\s*-\s*\d{1,2}\s*-\s*\d{1,2}|\d{1,2}\s*[-/]\s*\d{1,2}\s*[-/]\s*\d{4}))(?!\d)",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static List<string> Names(string text) =>
        Regex.Split(text.Trim(), @"\r\n|\r|\n|[ \t]{2,}|;")
            .Select(name => Regex.Replace(Regex.Replace(name.Trim(), @"\s*-\s*", "-"), @"\s+", " "))
            .Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static List<Entry> Read(string namesText, string adrText, HashSet<string> knownNames,
        string location, List<string> warnings)
    {
        var matches = AdrPattern.Matches(adrText).Cast<Match>().ToList();
        var labels = matches.Select(m => m.Groups["label"].Value).Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var names = Names(namesText).SelectMany(name => SplitCombinedName(name, labels, knownNames))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var candidates = matches.Select(match => names.Where(name =>
            match.Groups["label"].Length == 0 ? names.Count == 1 : LabelMatches(name, match.Groups["label"].Value)).ToList()).ToList();
        ResolveCompleteAssignment(candidates, labels.Count, names.Count);
        var dates = names.ToDictionary(name => name, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        var uncertain = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var position = 0;
        foreach (var (match, index) in matches.Select((match, index) => (match, index)))
        {
            CheckFragment(adrText[position..match.Index]);
            position = match.Index + match.Length;
            var normalizedDate = Regex.Replace(match.Groups["date"].Value, @"\s+", "");
            var valid = DateOnly.TryParseExact(normalizedDate,
                ["yyyy-M-d", "d-M-yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
            if (!valid)
            {
                uncertain.UnionWith(candidates[index]);
                warnings.Add($"{location}: ogiltigt ADR-datum {normalizedDate}; kontrollera raden manuellt.");
            }
            else if (candidates[index].Count == 1)
                dates[candidates[index][0]].Add(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            else
            {
                uncertain.UnionWith(candidates[index]);
                warnings.Add($"{location}: ADR-datum {normalizedDate} med etikett '{match.Groups["label"].Value}' kan inte kopplas entydigt till en chaufför.");
            }
        }
        CheckFragment(adrText[position..]);
        if (names.Count == 0 && adrText.Trim().Length > 0)
            warnings.Add($"{location}: ADR-datum finns utan chaufförsnamn.");

        return names.Select(name =>
        {
            var unique = dates[name].Distinct().ToList();
            if (unique.Count > 1)
                warnings.Add($"{location}: motstridiga ADR-datum för {name}; datum lämnas tomt.");
            return new Entry(name, unique.Count == 1 && !uncertain.Contains(name) ? unique[0] : "",
                unique.Count > 1);
        }).ToList();

        void CheckFragment(string fragment)
        {
            if (fragment.All(c => char.IsWhiteSpace(c) || c is ';' or ',')) return;
            warnings.Add($"{location}: okänt format i ADR-fältet '{fragment.Trim()}'; kontrollera den delen manuellt.");
            // An unreadable date can conflict with another date for the same label.
            // A separate '?' or comment must not discard other clearly labelled dates.
            if (fragment.Any(char.IsDigit))
            {
                var label = Regex.Match(fragment, @"(?<label>\p{L}+)\s*[:.]");
                uncertain.UnionWith(label.Success
                    ? names.Where(n => LabelMatches(n, label.Groups["label"].Value))
                    : names);
            }
        }
    }

    private static IEnumerable<string> SplitCombinedName(string name, List<string> labels, HashSet<string> knownNames)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var splits = new List<(string First, string Second)>();
        for (var index = 2; index <= words.Length - 2; index++)
        {
            var first = string.Join(" ", words[..index]);
            var second = string.Join(" ", words[index..]);
            var firstLabels = labels.Where(l => LabelMatches(first, l)).ToList();
            var secondLabels = labels.Where(l => LabelMatches(second, l)).ToList();
            var bothKnown = knownNames.Contains(first) && knownNames.Contains(second);
            var separatelyLabelled = firstLabels.Any(a => secondLabels.Any(b => !a.Equals(b, StringComparison.OrdinalIgnoreCase)));
            var knownLabelledSuffix = firstLabels.Count == 0 && secondLabels.Count > 0 && knownNames.Contains(second);
            if (bothKnown || separatelyLabelled || knownLabelledSuffix) splits.Add((first, second));
        }
        return splits.Count == 1 ? [splits[0].First, splits[0].Second] : [name];
    }

    private static void ResolveCompleteAssignment(List<List<string>> candidates, int labelCount, int nameCount)
    {
        if (candidates.Count != nameCount || labelCount != nameCount || candidates.Any(c => c.Count == 0)) return;
        var remaining = candidates.Select(c => c.ToList()).ToList();
        while (true)
        {
            var assigned = remaining.Where(c => c.Count == 1).Select(c => c[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (assigned.Count != remaining.Count(c => c.Count == 1)) return;
            var changed = false;
            foreach (var list in remaining.Where(c => c.Count > 1))
            {
                changed |= list.RemoveAll(assigned.Contains) > 0;
                if (list.Count == 0) return;
            }
            if (!changed) break;
        }
        if (remaining.All(c => c.Count == 1)
            && remaining.Select(c => c[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count() == nameCount)
            for (var index = 0; index < candidates.Count; index++) candidates[index] = remaining[index];
    }

    private static bool LabelMatches(string name, string label)
    {
        var words = name.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
        var initials = string.Concat(words.Select(word => word[0]));
        var firstLast = $"{words[0][0]}{words[^1][0]}";
        return label.Equals(words[0][..1], StringComparison.OrdinalIgnoreCase)
            || label.Equals(initials, StringComparison.OrdinalIgnoreCase)
            || label.Equals(firstLast, StringComparison.OrdinalIgnoreCase)
            || (label.Length > 1 && words[0].StartsWith(label, StringComparison.OrdinalIgnoreCase));
    }

    internal sealed record Entry(string Name, string Expiry, bool Conflicting);
}

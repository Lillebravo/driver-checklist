using System.Globalization;
using System.Text.RegularExpressions;

namespace DriverChecklist.Api.Services;

internal static class VehicleCellReader
{
    private static readonly Regex Registrations = new(
        @"(?<![A-Z0-9])(?<reg>(?:\d{3}\s*[A-Z]{2,3}|[A-Z]{2}\s+[A-Z]{2,3}\s*\d{2}|[A-Z]\s+[A-Z]{3}\s*\d{3}|[A-Z]{2}[\s-]*\d{1,2}[\s-]*[A-Z]{1,2}|[A-Z]{1,4}\s*:?\s*\d{2,6}\s*[A-Z]?(?!\d)|\d{1,2}[\s-]*[A-Z]{2,3}[\s:-]*\d{1,3}|\d[A-Z]{2,3}\s*\d{1,3}|[A-Z]\d\s*[A-Z]{2,3}\s*\d{2}|[A-Z]{2}-\d{2,3}-[A-Z]{2}|\d{2}-[A-Z]{3}-\d|[A-Z]\d[A-Z]{2}\d{2}))(?=$|[\s:?,(/)]|ADR|L[0-9G])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex Dates = new(
        @"(?<!\d)(?:\d{4}\s*-\s*\d{2}\s*-\s*\d{2}|\d{2}\s*-\s*\d{2}\s*-\s*\d{4}|\d{2}\s*-\s*\d{2}\s*-\s*\d{2})(?!\d)",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex Codes = new(
        @"ADR|L(?:\d(?:,\d)?|G)[A-Z]{2}(?:\s*\([^)]*\)|\+BH|\+)?|T(?:E)?\d{2}\+?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex Container = new(
        @"\bCont(?:ainer)?\.?\s*(?:nr)?\s*:?\s*(?<details>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    public static List<Entry> Read(string text, string location, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var container = Container.Match(text);
        var containerNumber = "";
        var containerCode = "";
        if (container.Success)
        {
            var detail = container.Groups["details"].Value.Trim();
            var code = Codes.Match(detail);
            containerCode = string.Join(" / ", Codes.Matches(detail).Cast<Match>().Select(value => value.Value.ToUpperInvariant()));
            var number = (code.Success ? detail[..code.Index] : detail).Trim().TrimEnd(':', ',', '/', '+').Trim();
            if (Regex.IsMatch(number, @"^[A-Z]{3,4}\s*\d[\d\s-]*$", RegexOptions.IgnoreCase)
                && number.Count(char.IsDigit) >= 5)
                containerNumber = Regex.Replace(number, @"\s+", "").ToUpperInvariant();
            else
                warnings.Add($"{location}: containernummer saknas eller är oklart: {detail}. Fyll i manuellt.");
            if (containerNumber.Length > 0 && !Regex.IsMatch(containerNumber, @"^[A-Z]{4}\d{6}-\d$"))
                warnings.Add($"{location}: avvikande containernummer {containerNumber}; kontrollera numret manuellt.");
            text = text[..container.Index];
        }

        var registrationText = Dates.Replace(text, match => new string(' ', match.Length));
        var matches = Registrations.Matches(registrationText).Cast<Match>()
            .Where(match =>
            {
                var value = Regex.Replace(match.Groups["reg"].Value, @"\s+", "");
                var code = Codes.Match(value);
                return !(code.Success && code.Index == 0 && code.Length == value.Length)
                    && !value.StartsWith("ADR", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
        var entries = new List<Entry>();
        if (matches.Count == 0)
        {
            if (container.Success && containerNumber.Length > 0)
                warnings.Add($"{location}: container {containerNumber} saknar identifierbart släp; välj TC och ange numret manuellt.");
            warnings.Add($"{location}: inget entydigt reg.nr kunde läsas: {text.Trim()}. Uppgiften måste kontrolleras manuellt.");
            return entries;
        }
        var prefix = text[..matches[0].Index].Trim(' ', '\r', '\n', '(', ':');
        if (prefix.Length > 0 && !Regex.IsMatch(prefix, @"^(Trailer|Link|Tr|SL|Dolly)\s*:?$", RegexOptions.IgnoreCase))
            warnings.Add($"{location}: anteckning före reg.nr behöver kontrolleras: {prefix}.");

        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var end = index + 1 < matches.Count ? matches[index + 1].Index : text.Length;
            var details = Regex.Replace(text[(match.Index + match.Length)..end],
                @"\b(Link|Trailer|Tr|Dolly|SL)\s*:\s*$", "", RegexOptions.IgnoreCase);
            var registration = Regex.Replace(match.Groups["reg"].Value.Replace(":", ""), @"\s+", " ").Trim().ToUpperInvariant();
            var dateMatches = Dates.Matches(details).Cast<Match>().ToList();
            var expiry = "";
            if (dateMatches.Count == 1)
            {
                var date = dateMatches[0];
                var normalized = Regex.Replace(date.Value, @"\s+", "");
                if (DateOnly.TryParseExact(normalized, ["yyyy-MM-dd", "dd-MM-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    expiry = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                else warnings.Add($"{location}: {registration} har ogiltigt datum {date.Value}; datum lämnas tomt.");
            }
            else if (dateMatches.Count > 1)
                warnings.Add($"{location}: flera datum för {registration}; datum lämnas tomt.");
            var codeMatches = Codes.Matches(details).Cast<Match>().ToList();
            var tankCode = string.Join(" / ", codeMatches.Select(code => NormalizeCode(code.Value)).Distinct());
            if (codeMatches.Count > 0 && (codeMatches.Count > 1 || Regex.IsMatch(details, @"\bF(?:ack)?[.:]?\s*\d|\b[12]&3", RegexOptions.IgnoreCase)))
            {
                var lastCode = codeMatches[^1];
                var codeEnd = lastCode.Index + lastCode.Length;
                var suffix = Regex.Match(details[codeEnd..],
                    @"^\s*(?:VP\b|\bF(?:ack)?[.:\-]?\s*\d(?:\s*[&o-]\s*\d)*\b)*", RegexOptions.IgnoreCase);
                var description = Regex.Replace(Dates.Replace(details[..(codeEnd + suffix.Length)], ""),
                    @"\(?\b(Link|Trailer|Tr|SL|Dolly)\b\)?\s*:?", "", RegexOptions.IgnoreCase)
                    .Trim(' ', '\r', '\n', ':', '-', ',', ';', '/');
                tankCode = Regex.Replace(description, @"\s+", " ").ToUpperInvariant();
            }
            if (tankCode.Length == 0 || expiry.Length == 0)
            {
                var missing = tankCode.Length == 0
                    ? expiry.Length == 0 ? "tankkod och entydigt godkännandedatum" : "tankkod"
                    : "entydigt godkännandedatum";
                warnings.Add($"{location}: {registration} saknar {missing}; saknade fält lämnas tomma.");
            }
            var remainder = Codes.Replace(Dates.Replace(details, ""), "");
            remainder = Regex.Replace(remainder, @"\b(Link|Trailer|Tr|SL|Dolly)\b", "", RegexOptions.IgnoreCase);
            if (Regex.IsMatch(remainder, @"\p{L}{3,}|[?]"))
                warnings.Add($"{location}: anteckning för {registration} behöver kontrolleras: {details.Trim()}.");
            var before = text[(index == 0 ? 0 : matches[index - 1].Index + matches[index - 1].Length)..match.Index];
            var role = Regex.Match(before, @"\b(Link|Trailer)\s*:\s*$", RegexOptions.IgnoreCase);
            if (!role.Success)
                role = Regex.Match(details, @"^\s*:?\s*\(?\s*(Link|Trailer)\b|\b(Link|Trailer)\s*[,;)]?\s*$", RegexOptions.IgnoreCase);
            var roleText = role.Success ? role.Value : "";
            entries.Add(new Entry(registration, tankCode, expiry, null, null,
                roleText.Contains("Link", StringComparison.OrdinalIgnoreCase) ? 1
                : roleText.Contains("Trailer", StringComparison.OrdinalIgnoreCase) ? 2 : 0));
        }
        if (container.Success)
        {
            if (entries.Count == 1)
            {
                entries[0] = entries[0] with
                {
                    ContainerNumber = containerNumber.Length > 0 ? containerNumber : null,
                    ContainerTankCode = containerCode.Length > 0 ? containerCode : null,
                };
                warnings.Add($"{location}: container kopplas till släpet. Kontrollera tank-/besiktningsuppgifter manuellt; släpets godkännandedatum gäller inte containerns besiktning.");
            }
            else warnings.Add($"{location}: containern kan inte kopplas entydigt till flera släp; välj TC och ange containernummer manuellt.");
        }
        return entries.OrderBy(entry => entry.Position == 1 ? 0 : entry.Position == 2 ? 2 : 1).ToList();
    }

    internal sealed record Entry(string RegNr, string TankCode, string ApprovalExpiry,
        string? ContainerNumber, string? ContainerTankCode, int Position = 0);

    private static string NormalizeCode(string code) =>
        Regex.Replace(code, @"\s+", "").ToUpperInvariant();

    public static bool EquivalentTankCodes(string first, string second) =>
        TankCodeKey(first) == TankCodeKey(second);

    private static string TankCodeKey(string value)
    {
        var key = value.ToUpperInvariant();
        key = Regex.Replace(key, @"\(\+\)", "+");
        key = Regex.Replace(key, @"\bF(?:ACK)?\s*[.:\-]?\s*(?=\d)", "");
        key = Regex.Replace(key, @"\s+", "");
        key = Regex.Replace(key, @"(?<=[A-Z+)])[;,/](?=\d)", "");
        // Separators between complete codes do not change their meaning. Compartment
        // numbers and ranges are retained, so F1&3 is not silently equated with F1-3.
        return Regex.Replace(key, @"(?<=[A-Z+)])[;,/](?=(?:L[0-9G]|TE?\d|F\d))", "/");
    }
}

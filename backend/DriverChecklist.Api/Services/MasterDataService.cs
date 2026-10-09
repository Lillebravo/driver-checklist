using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DriverChecklist.Api.Configuration;
using DriverChecklist.Api.Data;
using DriverChecklist.Api.Models.MasterData;
using Microsoft.Extensions.Options;

namespace DriverChecklist.Api.Services;

/// <summary>
/// Läser fordonsregistret read-only från en lokal Excel-kopia, eller demodata
/// när ingen sökväg konfigurerats.
/// </summary>
public class MasterDataService : IMasterDataService
{
    private static readonly Regex AdrPattern = new(
        @"(?:(?<initials>\p{L}{1,3})\s*[:.]\s*)?(?<date>[0-9]{1,2}[-/][0-9]{1,2}[-/][0-9]{4})",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly MasterDataOptions _options;

    public MasterDataService(IOptions<MasterDataOptions> options)
    {
        _options = options.Value;
    }

    public InitDataResponse GetInitData()
    {
        if (string.IsNullOrWhiteSpace(_options.Path))
            return InitialDataStore.GetData();

        if (!System.IO.Path.IsPathFullyQualified(_options.Path)
            || !string.Equals(System.IO.Path.GetExtension(_options.Path), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("MasterData:Path måste vara en absolut lokal sökväg till en .xlsx-fil.");

        using var stream = new FileStream(_options.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        var trucks = new Dictionary<string, TruckInfo>(StringComparer.Ordinal);
        var trailers = new Dictionary<string, TrailerInfo>(StringComparer.Ordinal);
        var drivers = new Dictionary<string, DriverInfo>(StringComparer.OrdinalIgnoreCase);
        var uncertainDrivers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        var trailerPositions = new Dictionary<string, int>(StringComparer.Ordinal);
        var foundHeader = false;

        foreach (var sheet in workbook.Worksheets)
        {
            var header = sheet.RowsUsed().Take(30).FirstOrDefault(row =>
                row.CellsUsed().Any(cell => NormalizeHeader(cell.GetString()) == "bil")
                && row.CellsUsed().Any(cell => NormalizeHeader(cell.GetString()) == "släp/trailer"));
            if (header is null)
                continue;

            foundHeader = true;
            var truckColumn = HeaderColumn(header, "bil");
            var trailerColumn = HeaderColumn(header, "släp/trailer");
            var driverColumn = OptionalHeaderColumn(header, "chaufförer");
            var adrColumn = OptionalHeaderColumn(header, "adrkortdatum");
            var haulierColumn = OptionalHeaderColumn(header, "åkeri");
            if ((driverColumn is null) != (adrColumn is null))
                throw new InvalidDataException($"Blad '{sheet.Name}': Chaufförer och ADR Kort Datum måste finnas tillsammans.");
            if (driverColumn is null)
                warnings.Add($"Blad '{sheet.Name}': Chaufförer och ADR Kort Datum saknas; inga chaufförer importeras från bladet.");
            foreach (var row in sheet.RowsUsed().Where(row => row.RowNumber() > header.RowNumber()))
            {
                var truckText = row.Cell(truckColumn).GetString().Trim();
                var trailerText = row.Cell(trailerColumn).GetString().Trim();
                if (truckText.Length == 0 && trailerText.Length == 0)
                    continue;

                var location = $"Blad '{sheet.Name}', rad {row.RowNumber()}";
                var truckEntries = VehicleCellReader.Read(truckText, $"{location}, bil", warnings);
                TruckInfo? truck = null;
                if (truckEntries.Count == 1)
                {
                    var entry = truckEntries[0];
                    var truckKey = NormalizeRegistration(entry.RegNr);
                    if (!trucks.TryGetValue(truckKey, out truck))
                        truck = new TruckInfo(entry.RegNr, entry.TankCode, entry.ApprovalExpiry, []);
                    else truck = truck with
                    {
                        TankCode = MergeField(truck.TankCode, entry.TankCode, $"bil:{truckKey}:tankkod", location, conflicts, warnings),
                        ApprovalExpiry = MergeField(truck.ApprovalExpiry, entry.ApprovalExpiry, $"bil:{truckKey}:datum", location, conflicts, warnings),
                    };
                    trucks[truckKey] = truck;
                }
                else
                    warnings.Add($"{location}: bil saknas eller är otydlig; släp och chaufförer läses utan bilkoppling.");
                var trailerEntries = VehicleCellReader.Read(trailerText, $"{location}, Släp/Trailer", warnings);
                foreach (var trailerEntry in trailerEntries)
                {
                    var trailerKey = NormalizeRegistration(trailerEntry.RegNr);
                    if (!trailers.TryGetValue(trailerKey, out var trailer))
                    {
                        trailer = new TrailerInfo(trailerEntry.RegNr, trailerEntry.ApprovalExpiry, [], trailerEntry.TankCode);
                        trailers.Add(trailerKey, trailer);
                    }
                    else
                    {
                        trailer = trailer with
                        {
                            TankCode = MergeField(trailer.TankCode ?? "", trailerEntry.TankCode, $"släp:{trailerKey}:tankkod", location, conflicts, warnings),
                            ApprovalExpiry = MergeField(trailer.ApprovalExpiry, trailerEntry.ApprovalExpiry, $"släp:{trailerKey}:datum", location, conflicts, warnings),
                        };
                        trailers[trailerKey] = trailer;
                    }
                    if (trailerEntry.ContainerNumber is not null)
                    {
                        var number = MergeField(trailer.ContainerNumber ?? "", trailerEntry.ContainerNumber,
                            $"släp:{trailerKey}:containernummer", location, conflicts, warnings);
                        var code = MergeField(trailer.ContainerTankCode ?? "", trailerEntry.ContainerTankCode ?? "",
                            $"släp:{trailerKey}:containertankkod", location, conflicts, warnings);
                        trailer = trailer with { ContainerNumber = number, ContainerTankCode = code };
                        trailers[trailerKey] = trailer;
                    }

                    if (truck is not null && !truck.Trailers.Any(existing => NormalizeRegistration(existing.RegNr) == trailerKey))
                        truck.Trailers.Add(trailer);
                    if (truck is not null && trailerEntry.Position > 0)
                    {
                        var key = $"{NormalizeRegistration(truck.RegNr)}:{trailerKey}";
                        if (trailerPositions.TryGetValue(key, out var position) && position != trailerEntry.Position)
                        {
                            trailerPositions[key] = 0;
                            warnings.Add($"{location}: motstridiga Link/Trailer-roller för {trailerEntry.RegNr}; välj släpordning manuellt.");
                        }
                        else trailerPositions[key] = trailerEntry.Position;
                    }
                }

                if (driverColumn is not null && adrColumn is not null)
                {
                    ImportDrivers(
                        row.Cell(driverColumn.Value).GetString(),
                        AdrCellText(row.Cell(adrColumn.Value)),
                        haulierColumn is null ? "" : row.Cell(haulierColumn.Value).GetString().Trim(),
                        truck?.RegNr ?? "", location, drivers, uncertainDrivers, warnings);
                }
            }
        }

        if (!foundHeader)
            throw new InvalidDataException("Hittade inte kolumnerna 'bil' och 'Släp/Trailer' bland de första 30 använda raderna i något blad.");
        if (trucks.Count == 0 && trailers.Count == 0)
            throw new InvalidDataException("Fordonsregistret innehåller inga fordonsrader.");
        foreach (var truck in trucks.Values)
        {
            for (var index = 0; index < truck.Trailers.Count; index++)
                truck.Trailers[index] = trailers[NormalizeRegistration(truck.Trailers[index].RegNr)];
            var ordered = truck.Trailers.OrderBy(trailer =>
            {
                trailerPositions.TryGetValue($"{NormalizeRegistration(truck.RegNr)}:{NormalizeRegistration(trailer.RegNr)}", out var position);
                return position == 1 ? 0 : position == 2 ? 2 : 1;
            }).ToList();
            truck.Trailers.Clear();
            truck.Trailers.AddRange(ordered);
        }

        var defaults = InitialDataStore.GetData();
        return defaults with
        {
            Drivers = drivers.Values.ToList(),
            Trucks = trucks.Values.ToList(),
            Trailers = trailers.Values.ToList(),
            VehicleRegistrySource = System.IO.Path.GetFileName(_options.Path),
            ImportWarnings = warnings,
        };
    }

    private static string NormalizeHeader(string value) =>
        Regex.Replace(value, @"\s+", "").ToLowerInvariant();

    private static string NormalizeRegistration(string value) =>
        Regex.Replace(value, @"[\s-]+", "").ToUpperInvariant();

    private static int HeaderColumn(IXLRow row, string name)
    {
        var cells = row.CellsUsed().Where(cell => NormalizeHeader(cell.GetString()) == name).ToList();
        if (cells.Count != 1)
            throw new InvalidDataException($"Blad '{row.Worksheet.Name}': kolumnen '{name}' måste finnas exakt en gång.");
        return cells[0].Address.ColumnNumber;
    }

    private static int? OptionalHeaderColumn(IXLRow row, string name) =>
        row.CellsUsed().Any(cell => NormalizeHeader(cell.GetString()) == name) ? HeaderColumn(row, name) : null;

    private static string AdrCellText(IXLCell cell) =>
        cell.DataType == XLDataType.DateTime
            ? cell.GetDateTime().ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)
            : cell.GetString();

    private static string MergeField(string first, string second, string key, string location,
        HashSet<string> conflicts, List<string> warnings)
    {
        if (conflicts.Contains(key)) return "";
        if (first.Length > 0 && second.Length > 0 && first != second)
        {
            conflicts.Add(key);
            warnings.Add($"{location}: motstridiga uppgifter för {key} ({first} / {second}); fältet lämnas tomt för manuell kontroll.");
            return "";
        }
        return first.Length == 0 ? second : first;
    }

    private static bool IsSeparator(string value) =>
        value.All(character => char.IsWhiteSpace(character) || character is ';' or ',');

    private static void ImportDrivers(
        string namesText, string adrText, string haulier, string truckRegNr, string location,
        Dictionary<string, DriverInfo> drivers, HashSet<string> uncertainDrivers, List<string> warnings)
    {
        var names = Regex.Split(namesText.Trim(), @"\r\n|\r|\n|[ \t]{2,}|;")
            .Select(name => Regex.Replace(name.Trim(), @"\s+", " "))
            .Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var dates = names.ToDictionary(name => name, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var position = 0;
        var invalidAdrText = false;
        foreach (Match match in AdrPattern.Matches(adrText))
        {
            if (!IsSeparator(adrText[position..match.Index]))
                invalidAdrText = true;
            position = match.Index + match.Length;
            var initials = match.Groups["initials"].Value;
            var candidates = names.Where(name =>
                initials.Length == 0 ? names.Count == 1 : InitialsMatch(name, initials)).ToList();
            if (candidates.Count != 1)
            {
                ambiguous.UnionWith(candidates);
                warnings.Add($"{location}: ADR-datum kan inte kopplas entydigt till en chaufför; kontrollera raden manuellt.");
                continue;
            }
            if (!DateOnly.TryParseExact(match.Groups["date"].Value,
                ["d-M-yyyy", "dd-MM-yyyy", "d/M/yyyy", "dd/MM/yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                ambiguous.Add(candidates[0]);
                warnings.Add($"{location}: ogiltigt ADR-datum; kontrollera raden manuellt.");
                continue;
            }
            dates[candidates[0]].Add(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        if (!IsSeparator(adrText[position..]))
            invalidAdrText = true;
        if (invalidAdrText)
        {
            ambiguous.UnionWith(names);
            warnings.Add($"{location}: ADR-fältet innehåller ett okänt format; inga datum från raden används.");
        }
        if (names.Count == 0 && adrText.Trim().Length > 0)
            warnings.Add($"{location}: ADR-datum finns utan chaufförsnamn.");

        foreach (var name in names)
        {
            var uniqueDates = dates[name].Distinct().ToList();
            var expiry = uniqueDates.Count == 1 && !ambiguous.Contains(name) ? uniqueDates[0] : "";
            if (expiry.Length == 0)
                warnings.Add($"{location}: ADR-datum saknas eller är osäkert för {name}; verifiera och fyll i manuellt.");
            if (!drivers.TryGetValue(name, out var driver))
            {
                driver = new DriverInfo(name, expiry, haulier.Length == 0 ? null : haulier,
                    truckRegNr.Length > 0 ? [truckRegNr] : []);
            }
            else
            {
                if (expiry.Length > 0 && driver.AdrExpiry.Length > 0 && expiry != driver.AdrExpiry)
                {
                    uncertainDrivers.Add(name);
                    warnings.Add($"{location}: motstridiga ADR-datum för {name}; datum lämnas tomt.");
                }
                var trucks = driver.TruckRegNrs!;
                if (truckRegNr.Length > 0 && !trucks.Contains(truckRegNr))
                    trucks.Add(truckRegNr);
                var mergedHaulier = driver.Haulier;
                if (!string.Equals(mergedHaulier ?? "", haulier, StringComparison.OrdinalIgnoreCase))
                {
                    mergedHaulier = null;
                    warnings.Add($"{location}: flera eller saknade åkeriuppgifter för {name}; ange åkeri manuellt.");
                }
                driver = driver with
                {
                    AdrExpiry = uncertainDrivers.Contains(name) ? "" : driver.AdrExpiry.Length > 0 ? driver.AdrExpiry : expiry,
                    Haulier = mergedHaulier,
                };
            }
            drivers[name] = driver;
        }
    }

    private static bool InitialsMatch(string name, string initials)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var fullInitials = string.Concat(words.Select(word => word[0]));
        return string.Equals(words[0][..1], initials, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullInitials, initials, StringComparison.OrdinalIgnoreCase)
            || (initials.Length > 1 && words[0].StartsWith(initials, StringComparison.OrdinalIgnoreCase));
    }

}

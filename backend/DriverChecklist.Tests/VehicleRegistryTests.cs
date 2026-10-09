using System.Security.Cryptography;
using ClosedXML.Excel;
using DriverChecklist.Api.Configuration;
using DriverChecklist.Api.Services;
using Microsoft.Extensions.Options;

internal static class VehicleRegistryTests
{
    public static void ValidateFile(string path)
    {
        var hash = SHA256.HashData(File.ReadAllBytes(path));
        var data = new MasterDataService(Options.Create(new MasterDataOptions { Path = Path.GetFullPath(path) })).GetInitData();
        var trailers = data.Trailers ?? throw new InvalidOperationException("Imported trailer registry is missing.");
        Check(data.Trucks.Count > 0 && trailers.Count > 0, "Workbook must import vehicles.");
        Check(data.Trucks.All(t => t.RegNr.Any(char.IsLetter) && t.RegNr.Any(char.IsDigit)), "Truck registrations must not be numeric placeholders.");
        Check(trailers.All(t => t.RegNr.Any(char.IsLetter) && t.RegNr.Any(char.IsDigit)), "Trailer registrations must contain letters and digits.");
        Check(data.Trucks.Select(t => t.RegNr.Replace(" ", "").Replace("-", "").ToUpperInvariant()).Distinct().Count() == data.Trucks.Count,
            "Truck registrations must be deduplicated.");
        Check(trailers.Select(t => t.RegNr.Replace(" ", "").Replace("-", "").ToUpperInvariant()).Distinct().Count() == trailers.Count,
            "Trailer registrations must be deduplicated.");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Source workbook must remain unchanged.");
        Console.WriteLine($"PASS: read-only workbook import: {data.Trucks.Count} trucks, {trailers.Count} trailers, {data.Drivers.Count} drivers, {data.ImportWarnings!.Count} warnings.");
        Console.WriteLine($"ADR dates: {data.Drivers.Count(d => d.AdrExpiry.Length > 0)} known, {data.Drivers.Count(d => d.AdrExpiry.Length == 0)} unknown.");
        foreach (var warning in data.ImportWarnings)
            Console.WriteLine(warning);
    }
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"driver-checklist-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "Regnummer.xlsx");
        var service = new MasterDataService(Options.Create(new MasterDataOptions { Path = path }));
        try
        {
            WriteWorkbook(path, [
                ("MBP\n94C: L4BN -\n2027-03-19", "RHA 067: L4BN - 2027-03-22"),
                ("mbp94c: l4bn - 2027-03-19", "RHA067: L4BN - 2027-03-22"),
                ("ABC 123: L4BH - 2028-02-29", "RHA067: L4BN - 2027-03-22;\nXYZ 789: L4DH - 2028-04-01"),
                ("DEF 456: L4BH - 2028-03-01", ""),
            ]);
            var hash = SHA256.HashData(File.ReadAllBytes(path));
            var data = service.GetInitData();
            Check(data.VehicleRegistrySource == "Regnummer.xlsx", "Must identify the real workbook.");
            Check(data.Drivers.Count == 0, "Excel mode must not mix in demo drivers.");
            Check(data.Products.Count > 0 && data.Operators.Count > 0, "Must retain product/operator configuration.");
            Check(data.Trucks.Count == 3, "Must deduplicate registration numbers ignoring case and whitespace.");
            var truck = data.Trucks[0];
            Check(truck.RegNr == "MBP 94C" && truck.TankCode == "L4BN"
                && truck.ApprovalExpiry == "2027-03-19", "Truck fields must match the supplied row format.");
            Check(truck.Trailers.Count == 1, "Repeated rows must not duplicate trailers.");
            var trailer = truck.Trailers[0];
            Check(trailer.RegNr == "RHA 067" && trailer.TankCode == "L4BN"
                && trailer.ApprovalExpiry == "2027-03-22", "Trailer fields must match the supplied row format.");
            Check(trailer.Compartments.Count == 0, "Must not invent compartments or inspection dates.");
            Check(data.Trucks[1].Trailers.Count == 2 && data.Trucks[2].Trailers.Count == 0,
                "Must preserve multiple-trailer and trailer-free combinations.");
            Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Source workbook must remain unchanged.");
            using (var shared = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                Check(service.GetInitData().Trucks.Count == 3, "Must allow a shared read-only import.");

            WriteWorkbook(path, [("NEW 123: L4BN - 2029-01-01", "")]);
            Check(service.GetInitData().Trucks.Single().RegNr == "NEW 123",
                "A new request must read the replaced workbook, not stale cached data.");

            WriteWorkbook(path, [
                ("AAA 123: ADR 2027-06-17", "BBB 123: F.1&3 L4BN, F.2 L4BV(+) 2027-06-30"),
                ("CCC 123: L4BH: 2027-05-24", "DDD 123 L4BH: 2027-01-31"),
                ("EEE 123 ADR 2027-05-15", "FFF 123: Fack 1&3: L4BH (+)VP Fack 2: L4BH - 2026-09-30"),
                ("GGG 123:ADR 2026-09-01", "HHH 123: F:1o3 L4BH (NSL+LUT) F:2 L4DH (SVS) 2027-07-01"),
            ], driverRows: [
                ("Kent Test  Christian Example  Niklas Sample", "K:21-01-2028 C:17-02-2030 N:25-04-2027", "Carrier A"),
                ("Lindy Example", "LE:08-08-2030", "Carrier B"),
                ("Hans Sample", "H. 07-03-2027", "Carrier C"),
                ("Jan Example\nMikael Sample", "J:12/11/2028 M:04-03-2031", "Carrier D"),
            ]);
            var full = service.GetInitData();
            Check(full.Trucks[0].TankCode == "ADR", "Must read ADR and optional date separators.");
            Check(full.Trucks[0].Trailers[0].TankCode == "F.1&3 L4BN, F.2 L4BV(+)",
                "Compartment tank descriptions must be preserved, not flattened to an invented single code.");
            Check(full.Drivers.Count == 7, "Must import unambiguous drivers.");
            Check(full.Drivers.Single(d => d.Name == "Christian Example").AdrExpiry == "2030-02-17", "Must match initials, not date order.");
            var lindy = full.Drivers.Single(d => d.Name == "Lindy Example");
            Check(lindy.AdrExpiry == "2030-08-08" && lindy.Haulier == "Carrier B"
                && lindy.TruckRegNrs!.Single() == "CCC 123", "Must import full initials, carrier and truck association.");
            Check(full.Drivers.Single(d => d.Name == "Jan Example").AdrExpiry == "2028-11-12", "Slash dates must be day/month/year.");

            WriteWorkbook(path, [
                ("AAA 123: ADR 2027-01-01", ""),
                ("BBB 123: ADR 2027-01-01", ""),
                ("CCC 123: ADR 2027-01-01", ""),
                ("DDD 123: ADR 2027-01-01", ""),
                ("EEE 123: ADR 2027-01-01", ""),
            ], driverRows: [
                ("Mirko Example   Jan Sample   Lars Test   Marlene Example", "J:20-10-2029 L:10-05-2030 M:24-04-2028", "Carrier A"),
                ("Missing Date", "", "Carrier B"),
                ("Invalid Date", "I:31-02-2028", "Carrier C"),
                ("Conflicting Date", "C:01-02-2028", "Carrier D"),
                ("Conflicting Date", "C:02-02-2028", "Carrier E"),
            ]);
            var uncertain = service.GetInitData();
            Check(uncertain.Drivers.Single(d => d.Name == "Mirko Example").AdrExpiry == ""
                && uncertain.Drivers.Single(d => d.Name == "Marlene Example").AdrExpiry == "",
                "Ambiguous initials must never be assigned by position.");
            Check(uncertain.Drivers.Single(d => d.Name == "Jan Sample").AdrExpiry == "2029-10-20",
                "An ambiguous M date must not invalidate the unique J date.");
            Check(uncertain.Drivers.Single(d => d.Name == "Missing Date").AdrExpiry == ""
                && uncertain.Drivers.Single(d => d.Name == "Invalid Date").AdrExpiry == "",
                "Missing/invalid dates must stay blank.");
            var conflicting = uncertain.Drivers.Single(d => d.Name == "Conflicting Date");
            Check(conflicting.AdrExpiry == "" && conflicting.Haulier is null && conflicting.TruckRegNrs!.Count == 2,
                "Conflicting dates/carriers must stay unknown while truck associations are merged.");
            Check(uncertain.ImportWarnings!.Count >= 5, "Uncertain imports must report warnings.");

            WriteWorkbook(path, [
                ("AAA 123: ADR 2027-01-01", ""),
                ("BBB 123: ADR 2027-01-01", ""),
            ], driverRows: [
                ("Mirko Example  Marlene Sample", "Mi:29-01-2027 Ma:24-04-2028", "Carrier A"),
                ("Michael Example  Markus Sample", "ME:01-02-2029 MS:03-04-2030", "Carrier A"),
            ]);
            var labels = service.GetInitData();
            Check(labels.Drivers.Single(d => d.Name == "Mirko Example").AdrExpiry == "2027-01-29"
                && labels.Drivers.Single(d => d.Name == "Marlene Sample").AdrExpiry == "2028-04-24",
                "Unique first-name prefixes must be supported.");
            Check(labels.Drivers.Single(d => d.Name == "Michael Example").AdrExpiry == "2029-02-01"
                && labels.Drivers.Single(d => d.Name == "Markus Sample").AdrExpiry == "2030-04-03",
                "Unique first/last-name initials must be supported.");

            WriteWorkbook(path, [
                ("AAA 123: ADR 2027-01-01", ""),
                ("BBB 123: ADR 2027-01-01", ""),
                ("CCC 123: ADR 2027-01-01", ""),
                ("DDD 123: ADR 2027-01-01", ""),
            ], driverRows: [
                ("Mirko Example  Marlene Sample", "M:24-04-2028", "Carrier A"),
                ("mirko example", "M:29-01-2027", "Carrier A"),
                ("Marlene Sample", "", "Carrier A"),
                ("Marlene Sample", "Ma:24-04-2028", "Carrier A"),
            ]);
            var repeated = service.GetInitData();
            Check(repeated.Drivers.Count == 2, "The same full name must merge across rows ignoring case.");
            var mirko = repeated.Drivers.Single(d => d.Name == "Mirko Example");
            var marlene = repeated.Drivers.Single(d => d.Name == "Marlene Sample");
            Check(mirko.AdrExpiry == "2027-01-29" && mirko.TruckRegNrs!.Count == 2,
                "A clear date on another row must resolve a missing/ambiguous date, retaining all truck links.");
            Check(marlene.AdrExpiry == "2028-04-24" && marlene.TruckRegNrs!.Count == 3,
                "Later clear dates must fill all entries for the same name, without using an ambiguous M date.");
            Check(repeated.ImportWarnings!.Count > 0, "The original ambiguous row must still be reported for review.");
            Check(!repeated.ImportWarnings!.Any(w => w.Contains("ADR-datum saknas eller är osäkert för Marlene Sample")),
                "Missing dates resolved on a later duplicate must not produce misleading final missing-date warnings.");

            WriteWorkbook(path, [
                ("AAA 123 ADR", ""), ("BBB 123 ADR", ""), ("CCC 123 ADR", ""),
                ("DDD 123 ADR", ""), ("EEE 123 ADR", ""), ("FFF 123 ADR", ""),
                ("GGG 123 ADR", ""), ("HHH 123 ADR", ""), ("III 123 ADR", ""),
                ("JJJ 123 ADR", ""), ("KKK 123 ADR", ""), ("LLL 123 ADR", ""),
                ("MMM 123 ADR", ""),
            ], driverRows: [
                ("David Test  Daniel Example  Alex Middle Sample", "David: 2030-07-26 Daniel: 13 - 06 - 2028 AS:17-09-2031", "Carrier"),
                ("Henrik Example  Johan Sample  Michael Test", "H:29-12-2029 J:2026-04-01 M:17-11-2029", "Carrier"),
                ("Fredrik Example  Kim Sample", "F:12-04-2027 K: :20-02-2030", "Carrier"),
                ("Morten Example  Morten Sample", "M:08-09-2029 MS:06-12-2026", "Carrier"),
                ("Morten Example", "M:08-09-2029", "Carrier"),
                ("Jasmine Example", "17-03-2030", "Carrier"),
                ("Jasmine Example Nicklas Sample", "J:17-03-2030 N:27-05-2029", "Carrier"),
                ("Thomas Sample", "T:30-01-2029", "Carrier"),
                ("Aleksej Example Thomas Sample", "? T:30-01-2029", "Carrier"),
                ("John Example  Jane Sample", "J:01-01-2030", "Carrier"),
                ("Invalid Example  Valid Sample", "I:01-05-20231 V:14/01/2027", "Carrier"),
                ("Conflicting Sample", "CS:01-01-2030 CS:02-01-2030", "Carrier"),
                ("Conflicting Sample", "CS:01-01-2030", "Carrier"),
            ]);
            var adrFormats = service.GetInitData();
            Check(adrFormats.Drivers.Single(d => d.Name == "David Test").AdrExpiry == "2030-07-26"
                && adrFormats.Drivers.Single(d => d.Name == "Daniel Example").AdrExpiry == "2028-06-13"
                && adrFormats.Drivers.Single(d => d.Name == "Alex Middle Sample").AdrExpiry == "2031-09-17",
                "Full first-name labels, ISO dates, spaces and first/last initials must work.");
            Check(adrFormats.Drivers.Where(d => new[] { "Henrik Example", "Johan Sample", "Michael Test", "Kim Sample" }.Contains(d.Name))
                .All(d => d.AdrExpiry.Length > 0), "Mixed date formats and repeated colons must preserve independent dates.");
            Check(adrFormats.Drivers.Single(d => d.Name == "Morten Example").AdrExpiry == "2029-09-08"
                && adrFormats.Drivers.Single(d => d.Name == "Morten Sample").AdrExpiry == "2026-12-06",
                "Complete uniquely constrained initials must match M and MS to separate people.");
            Check(adrFormats.Drivers.Single(d => d.Name == "Nicklas Sample").AdrExpiry == "2029-05-27"
                && adrFormats.Drivers.Single(d => d.Name == "Jasmine Example").TruckRegNrs!.Count == 2,
                "Distinct ADR labels must safely separate concatenated complete driver names.");
            Check(adrFormats.Drivers.Single(d => d.Name == "Thomas Sample").AdrExpiry == "2029-01-30"
                && adrFormats.Drivers.Any(d => d.Name == "Aleksej Example")
                && adrFormats.Drivers.All(d => d.Name != "Aleksej Example Thomas Sample"),
                "A repeated complete name plus matching label resolves a missing name separator, without guessing the other ADR date.");
            Check(adrFormats.Drivers.Where(d => new[] { "John Example", "Jane Sample" }.Contains(d.Name))
                .All(d => d.AdrExpiry.Length == 0), "An incomplete ambiguous J label must stay unresolved.");
            Check(adrFormats.Drivers.Single(d => d.Name == "Invalid Example").AdrExpiry == ""
                && adrFormats.Drivers.Single(d => d.Name == "Valid Sample").AdrExpiry == "2027-01-14",
                "An invalid year must not be truncated or discard another clearly labelled date.");
            Check(adrFormats.Drivers.Single(d => d.Name == "Conflicting Sample").AdrExpiry == "",
                "Conflicting dates in one row must remain blank after later duplicates.");
            Check(adrFormats.ImportWarnings!.Count(w => w.Contains("ADR-datum saknas eller är osäkert för Jasmine Example")) == 0,
                "A resolved repeated driver must not retain a misleading missing-date warning.");
            WriteWorkbook(path, [("AAA 123 ADR", ""), ("BBB 123 ADR", "")], driverRows: [
                ("Arend Jan Test Sample", "A:25-09-2030", "Carrier"),
                ("Morten Example  Morten Sample", "M:01-01-2030 M:02-01-2030", "Carrier"),
            ]);
            var ambiguousNames = service.GetInitData();
            Check(ambiguousNames.Drivers.Single(d => d.Name == "Arend Jan Test Sample").AdrExpiry == "2030-09-25",
                "Long genuine driver names must not be split solely by word count.");
            Check(ambiguousNames.Drivers.Where(d => d.Name.StartsWith("Morten")).All(d => d.AdrExpiry == ""),
                "Repeated identical initials cannot be assigned by position.");

            WriteWorkbook(path, [
                ("ABC 123: ADR 2027-04-26", "XYZ 789: ADR 2027-09-01                        Cont. TEST 725001-0: L4BN"),
                ("DEF 456: ADR 2027-04-26", "UVW 123: ADR 2027-09-01\nCont. TEST 725002-0: L4BN"),
            ]);
            var containers = service.GetInitData();
            Check(containers.Trucks.Count == 2 && containers.Trucks.All(t => t.Trailers.Count == 1),
                "Container annotation must not prevent importing the actual trailer.");
            Check(containers.Trucks[0].Trailers[0].RegNr == "XYZ 789"
                && containers.Trucks[0].Trailers[0].ApprovalExpiry == "2027-09-01"
                && containers.Trucks[0].Trailers[0].TankCode == "ADR",
                "Container tank code/date must not overwrite the trailer fields.");
            Check(containers.Trucks[0].Trailers[0].ContainerNumber == "TEST725001-0"
                && containers.Trucks[0].Trailers[0].ContainerTankCode == "L4BN",
                "Container number and tank code must be attached to the trailer.");
            Check(containers.ImportWarnings!.Any(w => w.Contains("besiktningsuppgifter")),
                "Unimported container inspection details must be surfaced for verification.");
            WriteWorkbook(path, [
                ("ABC 123: ADR 2027-04-26", "XYZ 789: ADR 2027-09-01"),
                ("DEF 456: ADR 2027-04-26", "XYZ 789: ADR 2027-09-01 Cont. TEST 725001-0: L4BN"),
            ]);
            Check(service.GetInitData().Trucks.All(t => t.Trailers.Single().ContainerNumber == "TEST725001-0"),
                "Container metadata on a later row must update all links to the same trailer.");
            WriteWorkbook(path, [
                ("ABC 123: ADR 2027-04-26", "XYZ 789: ADR 2027-09-01 Cont. TEST 725001-0: L4BN"),
                ("DEF 456: ADR 2027-04-26", "XYZ 789: ADR 2027-09-01 Cont. TEST 725002-0: L4BN"),
            ]);
            Check(service.GetInitData().Trailers!.Single().ContainerNumber == "", "Conflicting container numbers stay blank.");
            WriteWorkbook(path, [
                ("ABC 123: ADR 2027-04-26", "XYZ 789: ADR 2027-09-01 Cont. UNKNOWN"),
            ]);
            Check(service.GetInitData().Trailers!.Single().ContainerNumber is null, "Unknown containers must not be invented.");

            WriteWorkbook(path, [("ABC 123: L4BN - 2027-02-29", "")]);
            Check(service.GetInitData().Trucks.Single().ApprovalExpiry == "", "Invalid dates stay blank.");
            WriteWorkbook(path, [("ABC 123", "XYZ789"), ("DN 21143", "DT 5914:")]);
            var registrationsOnly = service.GetInitData();
            Check(registrationsOnly.Trucks.Count == 2 && registrationsOnly.Trucks.All(t =>
                t.TankCode == "" && t.ApprovalExpiry == "" && t.Trailers.Single().TankCode == ""
                && t.Trailers.Single().ApprovalExpiry == ""),
                "Registration-only trucks and trailers must import with empty code and date.");
            Check(registrationsOnly.ImportWarnings!.Count(w => w.Contains("saknar tankkod")) == 4,
                "Missing vehicle fields must be reported without blocking the registry.");
            WriteWorkbook(path, [
                ("ABC123", "XYZ789"),
                ("ABC 123: ADR 2027-01-01", "XYZ 789: L4BN 2027-02-01"),
                ("ABC123", "XYZ789"),
            ]);
            var mergedVehicles = service.GetInitData().Trucks.Single();
            Check(mergedVehicles.TankCode == "ADR" && mergedVehicles.ApprovalExpiry == "2027-01-01"
                && mergedVehicles.Trailers.Single().TankCode == "L4BN"
                && mergedVehicles.Trailers.Single().ApprovalExpiry == "2027-02-01",
                "Missing duplicate fields must not conflict with or erase known fields.");
            WriteWorkbook(path, [("ABC 123: L4BN - 2027-01-01", "UNKNOWN")]);
            Check(service.GetInitData().Trucks.Single().Trailers.Count == 0, "Unknown trailers warn without inventing a plate.");
            WriteWorkbook(path, [("", "XYZ 789: L4BN - 2027-01-01")]);
            Check(service.GetInitData().Trucks.Count == 0 && service.GetInitData().Trailers!.Count == 1,
                "Trailer-only rows remain selectable without a fake truck.");
            WriteWorkbook(path, [
                ("ABC 123: L4BN - 2027-01-01", ""),
                ("ABC123: L4BN - 2028-01-01", ""),
                ("ABC123: L4BN - 2028-01-01", ""),
            ]);
            Check(service.GetInitData().Trucks.Single().ApprovalExpiry == "", "Conflicting dates must stay blank on later duplicates.");
            WriteWorkbook(path, [
                ("ABC 123: L4BN - 2027-01-01", "XYZ 789: L4BN - 2027-01-01"),
                ("DEF 456: L4BN - 2027-01-01", "XYZ789: L4DH - 2027-01-01"),
            ]);
            Check(service.GetInitData().Trucks.All(t => t.Trailers.Single().TankCode == ""),
                "Conflicting trailer codes clear all references without erasing dates.");

            WriteWorkbook(path, [
                ("AAA 123 ADR 2027-01-01", "BBB 123 (Link)L4BH - 2026-08-26, CCC 123(Trailer) L4BN - 2027-05-08"),
                ("DDD 123 ADR 2027-01-01", "Trailer: EEE 123: 2027-05-08 L4BN Link: FFF 123: L4BH 2026-08-26"),
                ("GGG 123", "HHH 123: L4BN / III 123: L4BH - 2026-10-28"),
                ("JJJ 123: 2025-03 ADR", "KKK 123: LGBH - 25-04-23"),
                ("LLL 123: ADR 2025- 04-12", "MMM 123: 2026-11-08 ADR Cont Nr TEST 029000-1 L4BN"),
                ("15 TST 1 ADR 2026-10-27", "OR-28-ZT ADR 2025-08-30 Contnr: TEST 350008-3 L4BN"),
                ("99", "NNN 123: L4BH 2027-09-17"),
                ("OOO 123: ADR", "PPP 123: L2,6BH Ok att lasta fast tankkod är annorlunda 2027-06-30"),
                ("QQQ 123 ADR 2027-01-01", "RRR 123: L4BN 2027-08-13 Link SSS 123: L4BH 2027-08-07 Trailer"),
                ("011 TST ADR 2027-01-01", "594 AB L4BH 2027-01-01"),
                ("TTT 123 ADR 2027-01-01", "OP 37 HK ADR 03-12-2026 Contnr: TEST 351033-3 T11/L4BN"),
                ("UUU 123 ADR 2027-01-01", "VVV 123: L4BH/TE11 2027-07-15"),
            ]);
            var varied = service.GetInitData();
            Check(varied.Trucks[0].Trailers.Select(t => t.RegNr).SequenceEqual(new[] { "BBB 123", "CCC 123" }),
                "Link/Trailer decorations must preserve both trailers.");
            Check(varied.Trucks[1].Trailers.Select(t => t.RegNr).SequenceEqual(new[] { "FFF 123", "EEE 123" }),
                "Link must be first and Trailer second, regardless of cell order.");
            Check(varied.Trucks[1].Trailers[0].ApprovalExpiry == "2026-08-26", "Each trailer keeps its own date.");
            Check(varied.Trucks[2].Trailers[0].ApprovalExpiry == "" && varied.Trucks[2].Trailers[1].ApprovalExpiry == "2026-10-28",
                "A date on trailer two must never be inferred for trailer one.");
            Check(varied.Trucks[3].ApprovalExpiry == "" && varied.Trucks[3].Trailers[0].ApprovalExpiry == "",
                "Partial dates and short years must stay blank.");
            Check(varied.Trucks[4].ApprovalExpiry == "2025-04-12"
                && varied.Trucks[4].Trailers[0].ContainerNumber == "TEST029000-1", "Reordered dates and alternate container labels work.");
            Check(varied.Trucks[5].RegNr == "15 TST 1" && varied.Trucks[5].Trailers[0].RegNr == "OR-28-ZT",
                "Foreign plates remain intact.");
            Check(varied.Trailers!.Any(t => t.RegNr == "NNN 123") && varied.Trucks.All(t => t.RegNr != "99"),
                "Numeric placeholders do not become truck registrations.");
            Check(varied.Trailers!.Single(t => t.RegNr == "PPP 123").TankCode == "L2,6BH",
                "Nonstandard explicit codes are retained without importing approval comments.");
            Check(varied.Trucks.Single(t => t.RegNr == "QQQ 123").Trailers.Select(t => t.RegNr)
                .SequenceEqual(new[] { "RRR 123", "SSS 123" }), "Suffix role labels must not bleed into the following entry.");
            Check(varied.Trucks.Single(t => t.RegNr == "011 TST").Trailers.Single().RegNr == "594 AB",
                "Digit-first foreign plates are supported.");
            Check(varied.Trailers!.Single(t => t.RegNr == "OP 37 HK").ContainerNumber == "TEST351033-3"
                && varied.Trailers!.Single(t => t.RegNr == "OP 37 HK").ApprovalExpiry == "2026-12-03",
                "T11 containers and explicit day-first four-digit dates are supported.");
            Check(varied.Trucks.Single(t => t.RegNr == "UUU 123").Trailers.Count == 1
                && varied.Trailers!.All(t => t.RegNr != "TE11"), "Tank codes must never become registration entries.");
            WriteWorkbook(path, [
                ("AAA 123 ADR 2027-01-01", "CCC 123 L4BN 2027-01-01 Trailer"),
                ("AAA 123 ADR 2027-01-01", "CCC 123 L4BN 2027-01-01 Trailer BBB 123 L4BH 2027-01-01 Link"),
            ]);
            Check(service.GetInitData().Trucks.Single().Trailers.Select(t => t.RegNr).SequenceEqual(new[] { "BBB 123", "CCC 123" }),
                "Link/Trailer ordering survives earlier duplicate rows.");
            WriteWorkbook(path, [("AAA 123 ADR 2027-01-01", "")],
                driverRows: [("Typed Date", "", "Carrier")]);
            using (var workbook = new XLWorkbook(path))
            {
                workbook.Worksheet("Vehicles").Cell("F4").Value = new DateTime(2030, 4, 25);
                workbook.Save();
            }
            Check(service.GetInitData().Drivers.Single().AdrExpiry == "2030-04-25", "Excel date-typed ADR cells are supported.");

            WriteWorkbook(path, [
                ("AAA 123: ADR 2027-06-17", "BBB 123: Fack 1&3: L4BH (+)VP Fack 2: L4BH - 2026-09-30"),
                ("AAA123: ADR 2027-06-17", "BBB123: Fack 1&3: L4BH (+)VP Fack 2: L4BH -2026-09-30"),
                ("CCC 123: ADR", "DDD 123: L4BH 2027-03-04 TST 685, dolly ?"),
                ("EEE 123", "FFF 123: L4BN F1& 3 L4BH F.2 2027-01-01"),
                ("EEE 123", "FFF 123: L4BN F.1&3 L4BH F2 2027-01-01"),
                ("GGG 123: ADR", "HHH 123: F1-3 L4BH F-2 L4BV+ 2027-01-01"),
                ("GGG 123: ADR", "HHH 123: F1-3 L4BH F.2 L4BV(+) 2027-01-01"),
                ("III 123: ADR", "JJJ 123: F1&3 L4BH F2 L4BN 2027-01-01"),
                ("III 123: ADR", "JJJ 123: 1&3 L4BH, 2 L4BN 2027-01-01"),
                ("KKK 123: ADR", "LLL 123: L4BN, L4BV+ 2027-01-01"),
                ("KKK 123: ADR", "LLL 123: L4BN / L4BV(+) 2027-01-01"),
            ]);
            var robust = service.GetInitData();
            Check(robust.Trucks[0].TankCode == "ADR" && robust.Trucks[0].ApprovalExpiry == "2027-06-17",
                "ADR must be preserved as an explicit truck code.");
            Check(robust.Trailers!.Single(t => t.RegNr == "BBB 123").TankCode == "FACK 1&3: L4BH (+)VP FACK 2: L4BH",
                "Reported trailer compartment description must survive whitespace and duplicate rows.");
            Check(robust.Trailers!.Single(t => t.RegNr == "DDD 123").ApprovalExpiry == "2027-03-04"
                && robust.Trailers!.Any(t => t.RegNr == "TST 685")
                && robust.Trailers!.All(t => t.RegNr != "04 TST 685"),
                "Dates must not be consumed as a digit-first prefix of the next registration.");
            Check(robust.Trailers!.Single(t => t.RegNr == "FFF 123").TankCode!.EndsWith("F.2"),
                "Compartment labels following a tank code must not be discarded.");
            Check(new[] { "FFF 123", "HHH 123", "JJJ 123", "LLL 123" }.All(reg =>
                !string.IsNullOrEmpty(robust.Trailers!.Single(t => t.RegNr == reg).TankCode)),
                "Equivalent punctuation, F labels and (+)/+ must not cause false conflicts.");
            Check(robust.Trucks.Single(t => t.RegNr == "EEE 123").TankCode == "",
                "Missing code must not be guessed as ADR.");
            WriteWorkbook(path, [
                ("AAA 123 ADR", "BBB 123 F1&3 L4BH F2 L4BN 2027-01-01"),
                ("AAA 123 ADR", "BBB 123 F1-3 L4BH F2 L4BN 2027-01-01"),
            ]);
            Check(service.GetInitData().Trailers!.Single().TankCode == "",
                "Compartment ranges must not silently be treated as selected compartment numbers.");
            ExpectInvalid(service, path, [], "inga fordonsrader");
            WriteWorkbook(path, [], trailerHeader: "Unknown");
            Expect<InvalidDataException>(() => service.GetInitData(), "Släp/Trailer");
            WriteWorkbook(path, [], duplicateHeader: true);
            Expect<InvalidDataException>(() => service.GetInitData(), "exakt en gång");
            File.Delete(path);
            Expect<FileNotFoundException>(() => service.GetInitData(), "");
            var invalidPathService = new MasterDataService(Options.Create(new MasterDataOptions { Path = "Regnummer.xlsx" }));
            Expect<InvalidDataException>(() => invalidPathService.GetInitData(), "absolut lokal");

            var demo = new MasterDataService(Options.Create(new MasterDataOptions())).GetInitData();
            Check(demo.VehicleRegistrySource is null && demo.Drivers.Count > 0 && demo.Trucks.Count > 0,
                "An unconfigured source must preserve existing demo mode.");
            Console.WriteLine("PASS: vehicle/driver registry parsing, initials, uncertain ADR dates, carriers, deduplication, errors, read-only access and demo mode.");
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private static void WriteWorkbook(
        string path, (string Truck, string Trailer)[] rows,
        string trailerHeader = "Släp/\nTrailer", bool duplicateHeader = false,
        (string Names, string Adr, string Haulier)[]? driverRows = null)
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Instructions").Cell("A1").Value = "Not a vehicle register";
        var sheet = workbook.AddWorksheet("Vehicles");
        sheet.Cell("A1").Value = "Vehicle register";
        sheet.Cell("A3").Value = "Datum";
        sheet.Cell("B3").Value = " bil ";
        sheet.Cell("C3").Value = trailerHeader;
        if (duplicateHeader)
            sheet.Cell("D3").Value = "bil";
        if (driverRows is not null)
        {
            sheet.Cell("E3").Value = "Chaufförer";
            sheet.Cell("F3").Value = "ADR\nKort Datum";
            sheet.Cell("G3").Value = "Åkeri";
        }
        for (var index = 0; index < rows.Length; index++)
        {
            sheet.Cell(index + 4, 2).Value = rows[index].Truck;
            sheet.Cell(index + 4, 3).Value = rows[index].Trailer;
            if (driverRows is not null)
            {
                sheet.Cell(index + 4, 5).Value = driverRows[index].Names;
                sheet.Cell(index + 4, 6).Value = driverRows[index].Adr;
                sheet.Cell(index + 4, 7).Value = driverRows[index].Haulier;
            }
        }
        workbook.SaveAs(path);
    }

    private static void ExpectInvalid(
        MasterDataService service, string path, (string Truck, string Trailer)[] rows, string message)
    {
        WriteWorkbook(path, rows);
        Expect<InvalidDataException>(() => service.GetInitData(), message);
    }

    private static void Expect<T>(Action action, string message) where T : Exception
    {
        try
        {
            action();
        }
        catch (T exception)
        {
            Check(exception.Message.Contains(message, StringComparison.OrdinalIgnoreCase), "Error must describe the failure.");
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}, not a demo fallback.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

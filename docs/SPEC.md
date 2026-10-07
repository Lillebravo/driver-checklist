# Ursprunglig Specifikation (Historisk Referens)

> Det här dokumentet är den ursprungliga kravspecifikationen och designskissen
> som detta projekt byggdes utifrån. Den bevaras oförändrad som historisk
> referens. Se [README.md](../README.md) för den löpande, uppdaterade
> beskrivningen av hur systemet faktiskt är implementerat.

# Utlastning Checklista Generator

Ett internt webbaserat verktyg för utlastning av kemikalier. Systemet slår upp chaufförer och fordonskombinationer från en delad Excel-fil (read-only), kontrollerar ADR-giltighet, hanterar kopplingar till dragbilar och upp till två släpvagnar, samt genererar och skriver ut rätt uppsättning färdigifyllda checklistor baserat på valda produkter och utlastningsstationer.

---

## 1. Huvudflöde i Gränssnittet

1. **Sök / Välj Chaufför:**
   * Operatören söker på chaufförens namn (autocomplete från Excel-arkivet).
   * Systemet visar chaufförens **ADR-utgångsdatum** direkt (med färgvarning om det närmar sig eller har passerat).
   * Om chauffören inte finns: Operatören anger manuellt namn och ADR-utgångsdatum. Chauffören flaggas internt som `Ny Chaufför`.
2. **Välj / Fyll i Fordonsekipage:**
   * När chauffören valts visas en lista över fordon som chauffören brukar köra.
   * Operatören kan välja en befintlig dragbil eller skriva in ett nytt registreringsnummer manuellt (flaggar som `Ny Dragbil`).
   * **Släpvagnar:** Operatören kan välja eller fylla i upp till **2 släpvagnar** (Släp 1 och Släp 2) med respektive besiktningsdatum, trycktestdatum och tankkod. Manuellt inmatade släp flaggas som `Nytt Släp 1` respektive `Nytt Släp 2`.
3. **Välj Produkter:**
   * Operatören bockar för vilka produkter som ska lastas under transporten.
4. **Generera & Skriv Ut:**
   * Motorn beräknar hur många och vilka checklistor som krävs baserat på utlastningsplatser och malltyper.
   * Vid nya chaufförer/fordon stämplas tydliga textmarkeringar (`[NY CHAUFFÖR]`, `[NY DRAGBIL]`, `[NYTT SLÄP 1]`, `[NYTT SLÄP 2]`) på checklistan så att transportledare vet att uppgifterna ska föras in i master-Excelen manuellt i efterhand.
   * Utskriftsjobb skickas till skrivaren via webbläsaren. Ingen data skrivs tillbaka till Excel-filen.

---

## 2. Produkter, UN-nummer & Stationer

### 2.1 Produktkatalog & Stationsregler

| Produktgrupp | Variant / Koncentration | UN-nr | Utlastningsplats (Station ID) | Checklistemall |
| :--- | :--- | :--- | :--- | :--- |
| **Saltsyra (SAS)** | Saltsyra | UN 1789 | `STATION_SAS` | **Typ 1** |
| **Svavelsyra (SVS 94–97 %)** | SVS 94–97 % | UN 1830 | `STATION_SVS_97` | **Typ 2** |
| **Svavelsyra (SVS 98 %)** | SVS 98 % | UN 1830 | `STATION_SVS_98_37` *(Gemensam)* | **Typ 2** |
| **Svavelsyra (SVS 37 %)** | SVS 37 % | UN 2796 | `STATION_SVS_98_37` *(Gemensam)* | **Typ 2** |
| **Fennosize (AKD)** | Fennosize KD 364M | UN 1760 | `STATION_AKD` | **Typ 2** |
| **Aluminiumsulfat (ALS)**| ALS | UN 3264 | `STATION_ALS` | **Typ 3** |
| **Natronlut (LUT)** | Natronlut | UN 1824 | `STATION_LUT` | **Typ 3** |
| **PIX** | PIX 111, 113, 118, 311 | UN 2582 / UN 3264 | `STATION_PIX` | **Typ 1** |
| **PAX** | PAX 15, 60, 100 | UN 1760 / UN 3264 | `STATION_PAX_BDP` *(Gemensam)* | **Typ 1** |
| **BDP** | BDP 865, 870 | UN 1760 / UN 3264 | `STATION_PAX_BDP` *(Gemensam)* | **Typ 1** |

---

### 2.2 Utskrifts- och Grupperingsregler

1. **Gemensamma utlastningar:**
   * **PAX & BDP:** Delar utlastningsplats (`STATION_PAX_BDP`) och använder **Typ 1**. Dessa kan samsas på en gemensam checklista.
   * **SVS 98 % & SVS 37 %:** Delar utlastningsplats (`STATION_SVS_98_37`) och använder **Typ 2**. Lastar en bil både 98 % och 37 % SVS krävs endast **en** checklista för dessa.
2. **Separata utlastningar:**
   * **SVS 94–97 %:** Har en egen station (`STATION_SVS_97`). Om en transport lastar både 97 % och 98 % genereras **två** separata checklistor av Typ 2.
   * **ALS & LUT:** Båda använder **Typ 3**, men har separata stationer (`STATION_ALS` respektive `STATION_LUT`) $\rightarrow$ kräver **två** separata checklistor av Typ 3.
   * **SAS & PIX:** Båda använder **Typ 1**, men har separata stationer (`STATION_SAS` respektive `STATION_PIX`) $\rightarrow$ kräver separata checklistor.
3. **Interna varianter:**
   * Olika produktnummer inom samma familj (t.ex. PIX 111 och PIX 113 eller PAX 15 och PAX 60) kräver inte separata checklistor sinsemellan då de delar station.
4. **Formel för antal checklistor:**
   $$\text{Antal checklistor} = \text{Antal unika kombinationer av } (\text{ChecklistType}, \text{LoadingStationId})$$

---

## 3. Ekipage- och Flagglogik (Ny Chaufför / Fordon)

Eftersom Excel-filen är delad via Microsoft Teams/SharePoint och öppnas i **read-only** sker inga databasskrivningar från systemet. För att uppmärksamma manuell registrering sätts tydliga textmarkeringar i fältet `Fält_StatusFlaggor`:

```text
               ┌────────────────────────────────────────────────────────┐
               │              STATUS FÖR REGISTRERING                   │
               │                                                        │
               │ [X] NY CHAUFFÖR   [ ] NY DRAGBIL   [X] NYTT SLÄP 1     │
               │ (Förs in manuellt i fordonsregistret av transportledare)│
               └────────────────────────────────────────────────────────┘
```

```csharp
public record Driver(
    string Name,
    DateTime? AdrExpiryDate,
    List<string> FrequentVehicleRegNos
);

public record VehicleUnit(
    string RegNr,
    string? Tankkod,
    DateTime? BesiktningDatum,
    DateTime? TrycktestDatum,
    bool IsNew // True om enheten matats in manuellt
);

public record TransportOrder(
    string DriverName,
    DateTime? DriverAdrExpiry,
    bool IsNewDriver,
    VehicleUnit Truck,
    VehicleUnit? Trailer1,
    VehicleUnit? Trailer2,
    List<ProductDefinition> SelectedProducts
);

public enum ChecklistType
{
    Type1_PixPaxSasBdp,
    Type2_SvsAkd,
    Type3_AlsLut
}

public record ProductDefinition(
    string Code,
    string DisplayName,
    string Family,
    string UnNumber,
    string LoadingStationId,
    ChecklistType TemplateType
);

public static class ProductCatalog
{
    public static readonly List<ProductDefinition> Products = new()
    {
        // Typ 1: SAS, PIX, PAX, BDP
        new("SAS", "Saltsyra", "SAS", "UN 1789", "STATION_SAS", ChecklistType.Type1_PixPaxSasBdp),

        new("PIX_111", "PIX 111", "PIX", "UN 2582", "STATION_PIX", ChecklistType.Type1_PixPaxSasBdp),
        new("PIX_113", "PIX 113", "PIX", "UN 2582", "STATION_PIX", ChecklistType.Type1_PixPaxSasBdp),
        new("PIX_118", "PIX 118", "PIX", "UN 2582", "STATION_PIX", ChecklistType.Type1_PixPaxSasBdp),
        new("PIX_311", "PIX 311", "PIX", "UN 2582", "STATION_PIX", ChecklistType.Type1_PixPaxSasBdp),

        new("PAX_15",  "PAX 15",  "PAX", "UN 1760", "STATION_PAX_BDP", ChecklistType.Type1_PixPaxSasBdp),
        new("PAX_60",  "PAX 60",  "PAX", "UN 1760", "STATION_PAX_BDP", ChecklistType.Type1_PixPaxSasBdp),
        new("PAX_100", "PAX 100", "PAX", "UN 1760", "STATION_PAX_BDP", ChecklistType.Type1_PixPaxSasBdp),

        new("BDP_865", "BDP 865", "BDP", "UN 1760", "STATION_PAX_BDP", ChecklistType.Type1_PixPaxSasBdp),
        new("BDP_870", "BDP 870", "BDP", "UN 1760", "STATION_PAX_BDP", ChecklistType.Type1_PixPaxSasBdp),

        // Typ 2: SVS och AKD
        new("SVS_97",     "Svavelsyra 94-97%", "SVS", "UN 1830", "STATION_SVS_97",    ChecklistType.Type2_SvsAkd),
        new("SVS_98",     "Svavelsyra 98%",    "SVS", "UN 1830", "STATION_SVS_98_37", ChecklistType.Type2_SvsAkd),
        new("SVS_37",     "Svavelsyra 37%",    "SVS", "UN 2796", "STATION_SVS_98_37", ChecklistType.Type2_SvsAkd),
        new("AKD_KD364M", "Fennosize KD 364M", "AKD", "UN 1760", "STATION_AKD",       ChecklistType.Type2_SvsAkd),

        // Typ 3: ALS och LUT
        new("ALS", "Aluminiumsulfat", "ALS", "UN 3264", "STATION_ALS", ChecklistType.Type3_AlsLut),
        new("LUT", "Natronlut",       "LUT", "UN 1824", "STATION_LUT", ChecklistType.Type3_AlsLut)
    };
}

// 1. Gruppera valda produkter per malltyp och fysisk utlastningsplats
var printJobs = order.SelectedProducts
    .GroupBy(p => new { p.TemplateType, p.LoadingStationId })
    .ToList();

foreach (var job in printJobs)
{
    // 2. Matcha mot rätt Excel-mall
    string templateFile = job.Key.TemplateType switch
    {
        ChecklistType.Type1_PixPaxSasBdp => "Mall_Checklista_Typ1.xlsx",
        ChecklistType.Type2_SvsAkd       => "Mall_Checklista_Typ2.xlsx",
        ChecklistType.Type3_AlsLut       => "Mall_Checklista_Typ3.xlsx",
        _ => throw new InvalidOperationException("Ogiltig malltyp")
    };

    // 3. Fyll i data via ClosedXML i minnet (MemoryStream)
    using var wb = new XLWorkbook(templateFile);
    var sheet = wb.Worksheet(1);

    // Chaufför & ADR
    sheet.Cell("Fält_ChaufförNamn").Value = order.DriverName;
    sheet.Cell("Fält_AdrUtgång").Value = order.DriverAdrExpiry?.ToString("yyyy-MM-dd") ?? "SAKNAS";

    // Dragbil
    sheet.Cell("Fält_Bil_RegNr").Value = order.Truck.RegNr;
    sheet.Cell("Fält_Bil_Tankkod").Value = order.Truck.Tankkod ?? "";
    sheet.Cell("Fält_Bil_Besiktning").Value = order.Truck.BesiktningDatum?.ToString("yyyy-MM-dd") ?? "";
    sheet.Cell("Fält_Bil_Trycktest").Value = order.Truck.TrycktestDatum?.ToString("yyyy-MM-dd") ?? "";

    // Släp 1
    if (order.Trailer1 != null)
    {
        sheet.Cell("Fält_Släp1_RegNr").Value = order.Trailer1.RegNr;
        sheet.Cell("Fält_Släp1_Tankkod").Value = order.Trailer1.Tankkod ?? "";
        sheet.Cell("Fält_Släp1_Besiktning").Value = order.Trailer1.BesiktningDatum?.ToString("yyyy-MM-dd") ?? "";
        sheet.Cell("Fält_Släp1_Trycktest").Value = order.Trailer1.TrycktestDatum?.ToString("yyyy-MM-dd") ?? "";
    }

    // Släp 2
    if (order.Trailer2 != null)
    {
        sheet.Cell("Fält_Släp2_RegNr").Value = order.Trailer2.RegNr;
        sheet.Cell("Fält_Släp2_Tankkod").Value = order.Trailer2.Tankkod ?? "";
        sheet.Cell("Fält_Släp2_Besiktning").Value = order.Trailer2.BesiktningDatum?.ToString("yyyy-MM-dd") ?? "";
        sheet.Cell("Fält_Släp2_Trycktest").Value = order.Trailer2.TrycktestDatum?.ToString("yyyy-MM-dd") ?? "";
    }

    // Produkter & UN-nummer för denna specifika checklista
    sheet.Cell("Fält_Produkter").Value = string.Join(", ", job.Select(p => p.DisplayName));
    sheet.Cell("Fält_UNNr").Value = string.Join(", ", job.Select(p => p.UnNumber).Distinct());

    // Statusflaggor (Ny chaufför / Ny dragbil / Nya släp)
    var flaggor = new List<string>();
    if (order.IsNewDriver) flaggor.Add("[NY CHAUFFÖR]");
    if (order.Truck.IsNew) flaggor.Add("[NY DRAGBIL]");
    if (order.Trailer1?.IsNew == true) flaggor.Add("[NYTT SLÄP 1]");
    if (order.Trailer2?.IsNew == true) flaggor.Add("[NYTT SLÄP 2]");

    sheet.Cell("Fält_StatusFlaggor").Value = flaggor.Count > 0 
        ? string.Join("  ", flaggor) 
        : "BEFINTLIG I SYSTEM";

    // 4. Skicka till utskriftsström (Duplex A4)
}
```

## 6. Namngivna Celler i Excel-mallarna (*Named Ranges*)
Samtliga tre Excel-mallar ska konfigureras med följande namngivna celler:

- `Fält_ChaufförNamn`
- `Fält_AdrUtgång`
- `Fält_StatusFlaggor`
- `Fält_Bil_RegNr`, `Fält_Bil_Tankkod`, `Fält_Bil_Besiktning`, `Fält_Bil_Trycktest`
- `Fält_Släp1_RegNr`, `Fält_Släp1_Tankkod`, `Fält_Släp1_Besiktning`, `Fält_Släp1_Trycktest`
- `Fält_Släp2_RegNr`, `Fält_Släp2_Tankkod`, `Fält_Släp2_Besiktning`, `Fält_Släp2_Trycktest`
- `Fält_Produkter`
- `Fält_UNNr`

## 7. Säkerhet & Fillåsning

- Master-Excelen öppnas enbart i läsläge med delad åtkomst:

```csharp
new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
```

Inga ändringar skrivs tillbaka till Excel-filen. Detta eliminerar risken för låsningar och filkorruption när filen är öppen av andra användare i Microsoft Teams eller SharePoint.

### Del 1: Backend (.NET 8 C# Minimal API)
Körs med NuGet-paketen:

```bash
dotnet add package ClosedXML
```

> **Anmärkning:** Koden nedan (det konkreta `Program.cs`-utkastet med cellreferenser
> A5/A6/A8/A9 osv. samt det tillhörande Angular-utkastet) är den version som faktiskt
> implementerades i den här MVP:n - se motsvarande moduler under
> [backend/](../backend) och [frontend/](../frontend). Den tidigare delen av det här
> dokumentet (named ranges, `TransportOrder`, tre mallar) beskriver den ursprungliga,
> mer ambitiösa designen som är tänkt att växas fram till i senare iterationer.

```csharp
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

// API för att hämta hårdkodad förvald data (Initial Master Data)
app.MapGet("/api/init-data", () => Results.Ok(InitialDataStore.GetData()));

// API för att generera och ladda ner färdigifylld Excel-fil
app.MapPost("/api/checklist/generate", ([FromBody] GenerateChecklistRequest req) =>
{
    // 1. Avgör vilken Excel-mall som ska användas
    string templatePath = req.TemplateType switch
    {
        ChecklistTemplate.Type1_PixPaxSasBdp => "Ny 1 Saltsyra , Pix, mm. Tankar MED skyddande beläggning.xlsx",
        ChecklistTemplate.Type2_SvsAkd => "Ny 2 Svavelsyra 94,97 och 98 Fennosize. Tankar UTAN skyddande beläggning.xlsx",
        _ => throw new ArgumentException("Ogiltig checklistetyp")
    };

    if (!File.Exists(templatePath))
        return Results.NotFound($"Kunde inte hitta mallfilen: {templatePath}");

    using var templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var workbook = new XLWorkbook(templateStream);
    var sheet = workbook.Worksheet(1);

    // --- HUVUD/INFO PÅ FRAMSIDAN ---
    // A5: Datum/Tid: ... Åkeri: ...
    sheet.Cell("A5").Value = $"Datum/Tid: {DateTime.Now:yyyy-MM-dd HH:mm}           Åkeri: {req.Akeri ?? ""}";

    // A6: Chaufförens namn: ... Reg nr. Bil-Släp: ...
    string regSummary = req.Truck.RegNr + (req.Trailers.Count > 0 ? " - " + string.Join("/", req.Trailers.Select(t => t.RegNr)) : "");
    sheet.Cell("A6").Value = $"Chaufförens namn: {req.DriverName,-35} Reg nr. Bil-Släp: {regSummary}";

    // A8 & A9: UN-nummer och Produktnamn
    sheet.Cell("A8").Value = $"Container/Järnvägsvagns nr:                     UN Nummer: {string.Join(", ", req.SelectedProducts.Select(p => p.UnNumber).Distinct())}";
    sheet.Cell("A9").Value = $"Produkt namn: {string.Join(", ", req.SelectedProducts.Select(p => p.Name))}";

    // Statusflaggor för nya aktörer
    var flags = new List<string>();
    if (req.IsNewDriver) flags.Add("[NY CHAUFFÖR]");
    if (req.Truck.IsNew) flags.Add("[NY DRAGBIL]");
    for (int i = 0; i < req.Trailers.Count; i++)
        if (req.Trailers[i].IsNew) flags.Add($"[NYTT SLÄP {i + 1}]");

    if (flags.Count > 0)
    {
        sheet.Cell("A7").Value = $"Kemira SAP Nr:                                Preliminär Mängd:            STATUS: {string.Join(" ", flags)}";
    }

    // --- KRYSSRUTOR & KONTROLLER UNDER CHECK-IN ---
    // A13: Markera [X] Vakt
    sheet.Cell("A13").Value = "Check in \n□ Fordonskontrollant  ☒ Vakt  □ Exp";

    // B13 t.o.m B19 (TT-kolumnen): Klicka i "Ja"
    for (int r = 14; r <= 19; r++)
    {
        sheet.Cell(r, 2).Value = "Ja"; // Kolumn B = TT Ja
    }

    // E15: Chaufförens ADR-giltighet
    sheet.Cell("E15").Value = $"Giltighet: {req.DriverAdrExpiry:yyyy-MM-dd}";

    // E16: Klicka i Självlastande Helassist
    sheet.Cell("E16").Value = "  ☒ Full assist   □ Halv assist\n  □ Själv lastn\n  (Ej godkänd självlastare ska lastas av Operatör)";

    // E18: Tankkoder Tank 1, 2, 3, 4 (Bil först, sedan släpens fack i ordning)
    sheet.Cell("E18").Value = 
        $"ADR/RID Tank kod el UN mobil Tank Instruktion:\n\n" +
        $"Tank 1:  {req.TankSlots.ElementAtOrDefault(0)?.TankCode ?? "-",-12} Tank 2: {req.TankSlots.ElementAtOrDefault(1)?.TankCode ?? "-",-12}\n\n" +
        $"Tank 3:  {req.TankSlots.ElementAtOrDefault(2)?.TankCode ?? "-",-12} Tank 4: {req.TankSlots.ElementAtOrDefault(3)?.TankCode ?? "-",-12}";

    // E19: Inspektionstyp (L eller P) & Datum
    string FormatInspection(TankSlot? slot)
    {
        if (slot == null || string.IsNullOrWhiteSpace(slot.LastInspectionMonthYear)) return "□ L  □ P  (      /      )";
        string lBox = slot.InspectionType == "L" ? "☒" : "□";
        string pBox = slot.InspectionType == "P" ? "☒" : "□";
        return $"{lBox} L  {pBox} P  ({slot.LastInspectionMonthYear})";
    }

    sheet.Cell("E19").Value = 
        $"Inspektion Typ & (mån/år)\n" +
        $"Tank 1:  {FormatInspection(req.TankSlots.ElementAtOrDefault(0))}\n" +
        $"Tank 2:  {FormatInspection(req.TankSlots.ElementAtOrDefault(1))}\n" +
        $"Tank 3:  {FormatInspection(req.TankSlots.ElementAtOrDefault(2))}\n" +
        $"Tank 4:  {FormatInspection(req.TankSlots.ElementAtOrDefault(3))}";

    // --- BAKSIDAN (Signatur / Operatör) ---
    // A46: Kemira Fordonskontrollant / Exp / Vakten
    // Här fylls namnet på operatören/väkten i, samt datum i B46 och kryss i vakten
    sheet.Cell("A46").Value = $"Vakt: {req.OperatorName} (Kemira Fordonskontrollant/Exp/Vakten)";
    sheet.Cell("B46").Value = DateTime.Now.ToString("yyyy-MM-dd");

    var outputStream = new MemoryStream();
    workbook.SaveAs(outputStream);
    outputStream.Position = 0;

    return Results.File(
        outputStream,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Checklista_{req.Truck.RegNr}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
    );
});

app.Run();

// --- DTO-MODELLER ---
public record GenerateChecklistRequest(
    ChecklistTemplate TemplateType,
    string OperatorName,
    string DriverName,
    DateTime DriverAdrExpiry,
    bool IsNewDriver,
    string? Akeri,
    VehicleUnit Truck,
    List<VehicleUnit> Trailers,
    List<TankSlot> TankSlots,
    List<ProductItem> SelectedProducts
);

public record VehicleUnit(string RegNr, bool IsNew);
public record TankSlot(string TankCode, string InspectionType, string LastInspectionMonthYear, string ExpiryFormatted, bool IsExpired);
public record ProductItem(string Name, string UnNumber);

public enum ChecklistTemplate
{
    Type1_PixPaxSasBdp,
    Type2_SvsAkd
}

// --- FÖRVALDA MASTERDATA ---
public static class InitialDataStore
{
    public static object GetData() => new
    {
        DefaultOperator = "Vakt",
        Drivers = new[]
        {
            new { Name = "Tony Blaffert", AdrExpiry = "2027-02-01" },
            new { Name = "Håkan Tyrander", AdrExpiry = "2030-03-31" }
        },
        Trucks = new[]
        {
            new
            {
                RegNr = "JAF 58E",
                TankCode = "ADR",
                ApprovalExpiry = "2027-03-13",
                Trailers = new[]
                {
                    new {
                        RegNr = "CSU 474",
                        ApprovalExpiry = "2027-07-01",
                        // Fack 1 & 3: L4BH, Fack 2: L4DH, Trycktest: 2025-06P (Nästa är L)
                        Compartments = new[] {
                            new { CompartmentNo = 1, TankCode = "L4BH", LastTest = "2025-06", TestType = "P", IsTankContainer = false },
                            new { CompartmentNo = 2, TankCode = "L4DH", LastTest = "2025-06", TestType = "P", IsTankContainer = false },
                            new { CompartmentNo = 3, TankCode = "L4BH", LastTest = "2025-06", TestType = "P", IsTankContainer = false }
                        }
                    },
                    new {
                        RegNr = "WMJ 180",
                        ApprovalExpiry = "2027-09-17",
                        Compartments = new[] {
                            new { CompartmentNo = 1, TankCode = "L4BH", LastTest = "2024-09", TestType = "P", IsTankContainer = false }
                        }
                    },
                    new {
                        RegNr = "EGN 351",
                        ApprovalExpiry = "2027-05-31",
                        Compartments = new[] {
                            new { CompartmentNo = 1, TankCode = "L4BH", LastTest = "2024-05", TestType = "P", IsTankContainer = false }
                        }
                    },
                    new {
                        RegNr = "DMA 570",
                        ApprovalExpiry = "2027-05-05",
                        Compartments = new[] {
                            new { CompartmentNo = 1, TankCode = "L4BH", LastTest = "2024-06", TestType = "P", IsTankContainer = false }
                        }
                    }
                }
            }
        },
        Products = new[]
        {
            new { Id = "SAS", Name = "Saltsyra", Un = "UN 1789", Station = "SAS", Template = ChecklistTemplate.Type1_PixPaxSasBdp },
            new { Id = "PIX_111", Name = "PIX 111", Un = "UN 2582", Station = "PIX", Template = ChecklistTemplate.Type1_PixPaxSasBdp },
            new { Id = "PIX_113", Name = "PIX 113", Un = "UN 2582", Station = "PIX", Template = ChecklistTemplate.Type1_PixPaxSasBdp },
            new { Id = "PAX_15", Name = "PAX 15", Un = "UN 1760", Station = "PAX_BDP", Template = ChecklistTemplate.Type1_PixPaxSasBdp },
            new { Id = "PAX_60", Name = "PAX 60", Un = "UN 1760", Station = "PAX_BDP", Template = ChecklistTemplate.Type1_PixPaxSasBdp },
            new { Id = "BDP_865", Name = "BDP 865", Un = "UN 1760", Station = "PAX_BDP", Template = ChecklistTemplate.Type1_PixPaxSasBdp },
            new { Id = "SVS_97", Name = "Svavelsyra 94-97%", Un = "UN 1830", Station = "SVS_97", Template = ChecklistTemplate.Type2_SvsAkd },
            new { Id = "SVS_98", Name = "Svavelsyra 98%", Un = "UN 1830", Station = "SVS_98_37", Template = ChecklistTemplate.Type2_SvsAkd },
            new { Id = "SVS_37", Name = "Svavelsyra 37%", Un = "UN 2796", Station = "SVS_98_37", Template = ChecklistTemplate.Type2_SvsAkd },
            new { Id = "AKD_364M", Name = "Fennosize KD 364M", Un = "UN 1760", Station = "AKD", Template = ChecklistTemplate.Type2_SvsAkd }
        }
    };
}
```

### Egenskaper i denna MVP:

1. **Två Excel-mallar stöds direkt:** Typ 1 (Saltsyra, PIX, PAX, BDP) och Typ 2 (Svavelsyra 94-98 %, AKD KD 364M).
2. **Klickar i alla kravpunkter:**
   - Kryssar i `☒ Vakt` under Check-in.
   - Sätter `Ja` på TT för alla punkter i check-in.
   - Kryssar `☒ Full assist` vid självlastning.
   - Fyller i operatörsnamnet på baksidan (A46).
3. **Smart inspektionsberäkning:**
   - Lägger automatiskt på **3 år** för tankbil/tanktrailer (`TT`) och **2,5 år** om det är tankcontainer (`TC`).
   - Växlar varannan gång mellan **L** och **P** samt varnar i gränssnittet om datumet passerat.
4. **Tank 1–4 i exakt ordning:**
   - Tank 1 = Dragbil (`ADR`).
   - För släp med flera fack (t.ex. CSU 474: fack 1, 2, 3) läggs facken som Tank 2 (`L4BH`), Tank 3 (`L4DH`) och Tank 4 (`L4BH`).

> Angular/TypeScript-frontendutkastet som hörde ihop med ovanstående backend
> (`app.component.ts/html/css`) har implementerats modulärt - se
> [frontend/src/app](../frontend/src/app) för den uppdelade, komponentbaserade
> versionen istället för en enda monolitisk komponent.

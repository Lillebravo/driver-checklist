using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DriverChecklist.Api.Models;
using DriverChecklist.Api.Models.Enums;

namespace DriverChecklist.Api.Services;

/// <summary>
/// MVP-implementationen av checklistegenerering via ClosedXML. Mallen öppnas
/// alltid read-only med delad åtkomst (se README.md avsnitt 7) så att andra
/// användare kan ha filen öppen samtidigt i Teams/SharePoint - inga ändringar
/// skrivs någonsin tillbaka till mallfilen på disk.
///
/// Principen genomgående: vi läser mallens EGEN originaltext cell för cell och
/// gör riktade insättningar/kryssningar i den texten (istället för att skriva
/// över hela cellen med en nyuppbyggd sträng). Det bevarar mallens befintliga
/// etiketter, mellanrum/positionering och kryssrutor exakt som de ser ut i
/// Excel-filen, och är anledningen till att t.ex. "Åkeri:" och kryssrutorna
/// för UN-nummer/produktnamn nu hamnar rätt istället för att skrivas över.
/// </summary>
public class ChecklistGeneratorService : IChecklistGeneratorService
{
    private readonly ITemplateResolver _templateResolver;

    public ChecklistGeneratorService(ITemplateResolver templateResolver)
    {
        _templateResolver = templateResolver;
    }

    public ChecklistFile? Generate(GenerateChecklistRequest request)
    {
        var templatePath = _templateResolver.ResolveTemplatePath(request.TemplateType);
        if (templatePath is null || !File.Exists(templatePath))
        {
            return null;
        }

        using var templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(templateStream);
        var sheet = workbook.Worksheet(1);

        FillHeaderAndStatus(sheet, request);
        FillCheckIn(sheet, request);
        FillRoleCheckboxes(sheet, request);
        FillUtcheckning(sheet, request);
        FillSignature(sheet, request);

        var outputStream = new MemoryStream();
        workbook.SaveAs(outputStream);
        outputStream.Position = 0;

        var fileName = $"Checklista_{request.Truck.RegNr}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return new ChecklistFile(
            outputStream,
            fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static void FillHeaderAndStatus(IXLWorksheet sheet, GenerateChecklistRequest req)
    {
        // A5: "Datum/Tid: ... Åkeri: ..." - sätt in värdena direkt efter
        // respektive etikett så att mallens egen mellanrumsbredd bevaras
        // (annars hamnar "Åkeri:" fel positionerat, precis som tidigare).
        var a5 = sheet.Cell("A5").GetString();
        a5 = ReplaceAfterLabel(a5, "Datum/Tid:", $" {DateTime.Now:yyyy-MM-dd HH:mm}");
        a5 = ReplaceAfterLabel(a5, "Åkeri:", $" {req.Akeri}");
        sheet.Cell("A5").Value = a5;

        // A6: "Chaufförens namn: ... Reg nr. Bil-Släp: ..." - samma princip.
        var regSummary = req.Truck.RegNr +
            (req.Trailers.Count > 0 ? " - " + string.Join("/", req.Trailers.Select(t => t.RegNr)) : "");
        var a6 = sheet.Cell("A6").GetString();
        a6 = ReplaceAfterLabel(a6, "Chaufförens namn:", $" {req.DriverName}");
        a6 = ReplaceAfterLabel(a6, "Reg nr. Bil-Släp:", $" {regSummary}");
        sheet.Cell("A6").Value = a6;

        // A7: Kemira SAP Nr / Preliminär Lastnings Mängd - lämnas orört (fylls
        // i för hand), men statusflaggor för nya aktörer läggs till sist i raden.
        var flags = new List<string>();
        if (req.IsNewDriver) flags.Add("[NY CHAUFFÖR]");
        if (req.Truck.IsNew) flags.Add("[NY DRAGBIL]");
        for (var i = 0; i < req.Trailers.Count; i++)
        {
            if (req.Trailers[i].IsNew) flags.Add($"[NYTT SLÄP {i + 1}]");
        }

        if (flags.Count > 0)
        {
            var a7 = sheet.Cell("A7").GetString();
            sheet.Cell("A7").Value = $"{a7}   STATUS: {string.Join(" ", flags)}";
        }

        // A8: UN Nummer - kryssa i rätt ruta/rutor utifrån valda produkters
        // UN-nummer, utan att röra "Container/Järnvägsvagns nr:"-etiketten.
        var a8 = sheet.Cell("A8").GetString();
        sheet.Cell("A8").Value = TickUnNumberCheckboxes(a8, req.SelectedProducts);

        // A9: Produkt namn - kryssa i rätt produktfamilj och fyll i ev. variant
        // (t.ex. "111" för PIX 111) i mallens befintliga tomrum.
        var a9 = sheet.Cell("A9").GetString();
        sheet.Cell("A9").Value = TickProductCheckboxes(a9, req.SelectedProducts);
    }

    private static void FillCheckIn(IXLWorksheet sheet, GenerateChecklistRequest req)
    {
        // A13: Kryssa i "Vakt" (operatören i appen motsvarar alltid denna roll).
        var a13 = sheet.Cell("A13").GetString();
        sheet.Cell("A13").Value = TickCheckbox(a13, "Vakt");

        // B14 t.o.m B19 (TT-kolumnen): markera med ett stort, fetstilt X som
        // fyller hela rutan, istället för det tidigare lilla ordet "Ja"
        // - dessa kontrollpunkter är sådana systemet redan vet svaret på.
        for (var r = 14; r <= 19; r++)
        {
            var cell = sheet.Cell(r, 2);
            cell.Value = "X";
            cell.Style.Font.FontSize = 24;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // E15: Chaufförens ADR-giltighet.
        var e15 = sheet.Cell("E15").GetString();
        sheet.Cell("E15").Value = ReplaceAfterLabel(e15, "Giltighet:", $" {req.DriverAdrExpiry:yyyy-MM-dd}");

        // E16: Kryssa i vald lastningsassistans (redigerbar i Visa/Redigera-modalen).
        // Unspecified (ny/okänd chaufför) kryssar medvetet ingenting alls.
        if (req.AssistType != AssistType.Unspecified)
        {
            var e16 = sheet.Cell("E16").GetString();
            sheet.Cell("E16").Value = TickCheckbox(e16, AssistMarker(req.AssistType));
        }

        // E18: Tankkoder Tank 1, 2, 3, 4 (Bil först, sedan släpens fack i ordning)
        sheet.Cell("E18").Value =
            "ADR/RID Tank kod el UN mobil Tank Instruktion:\n\n" +
            $"Tank 1:  {req.TankSlots.ElementAtOrDefault(0)?.TankCode ?? "-",-12} Tank 2: {req.TankSlots.ElementAtOrDefault(1)?.TankCode ?? "-",-12}\n\n" +
            $"Tank 3:  {req.TankSlots.ElementAtOrDefault(2)?.TankCode ?? "-",-12} Tank 4: {req.TankSlots.ElementAtOrDefault(3)?.TankCode ?? "-",-12}";

        // E19: Inspektionstyp (L eller P) & Datum
        sheet.Cell("E19").Value =
            "Inspektion Typ & (mån/år)\n" +
            $"Tank 1:  {FormatInspection(req.TankSlots.ElementAtOrDefault(0))}\n" +
            $"Tank 2:  {FormatInspection(req.TankSlots.ElementAtOrDefault(1))}\n" +
            $"Tank 3:  {FormatInspection(req.TankSlots.ElementAtOrDefault(2))}\n" +
            $"Tank 4:  {FormatInspection(req.TankSlots.ElementAtOrDefault(3))}";
    }

    /// <summary>
    /// Kryssar i rätt Operatör/Chaufför-ruta vid de tre kontrollpunkterna
    /// längre ner i dokumentet (A21, A28, A33), baserat på samma
    /// lastningsassistans som valdes vid E16 - precis som pappersmallen
    /// själv kopplar ihop dem.
    /// </summary>
    private static void FillRoleCheckboxes(IXLWorksheet sheet, GenerateChecklistRequest req)
    {
        foreach (var address in new[] { "A21", "A28", "A33" })
        {
            var cell = sheet.Cell(address);
            var text = cell.GetString();
            var updated = req.AssistType switch
            {
                AssistType.FullAssist => TickCheckbox(text, "Operatör(Full assist)"),
                AssistType.HalfAssist => TickFirstMatch(text, "Halv assist"),
                AssistType.SelfLoading => TickFirstMatch(text, "Själv lastn", "Självlastning"),
                _ => text,
            };
            cell.Value = updated;
        }
    }

    private static void FillUtcheckning(IXLWorksheet sheet, GenerateChecklistRequest req)
    {
        // A42: Kryssa i "Vakt" vid utcheckning efter lastning, precis som vid check in.
        var a42 = sheet.Cell("A42").GetString();
        sheet.Cell("A42").Value = TickCheckbox(a42, "Vakt");
    }

    private static void FillSignature(IXLWorksheet sheet, GenerateChecklistRequest req)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        // Rad 46 (översta namnfältet): Vaktens namn, skrivet på en ny rad
        // under mallens befintliga text, högerjusterat i rutan.
        AppendNameBelow(sheet.Cell("A46"), req.OperatorName);
        sheet.Cell("B46").Value = today;

        // Rad 47 (andra namnfältet): "Operator el Chaufför (Självlastande)" -
        // endast vid Full assist är det operatören som utfört arbetet,
        // annars (Halv assist/Själv lastn/ospecificerat) är det chauffören.
        var row47Name = req.AssistType == AssistType.FullAssist ? req.OperatorName : req.DriverName;
        AppendNameBelow(sheet.Cell("A47"), row47Name);
        sheet.Cell("B47").Value = today;

        // Rad 48 (tredje namnfältet): lämnas helt orört (inget namn skrivs
        // in) - endast datumet fylls i.
        sheet.Cell("B48").Value = today;
    }

    /// <summary>
    /// Lägger till namnet på en ny rad under mallens befintliga instruktionstext
    /// och högerjusterar cellen, så att namnet hamnar längst ner till höger i
    /// rutan - utan att röra den ursprungliga texten eller tränga in i
    /// datum-/signaturcellerna bredvid (Excel klipper överflöd vid cellgränsen
    /// eftersom datumcellen bredvid alltid innehåller text).
    /// </summary>
    private static void AppendNameBelow(IXLCell cell, string name)
    {
        cell.Value = cell.GetString() + "\n" + name;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static string FormatInspection(TankSlot? slot)
    {
        if (slot is null || string.IsNullOrWhiteSpace(slot.LastInspectionMonthYear))
        {
            return "□ L  □ P  (      /      )";
        }

        var lBox = slot.InspectionType == "L" ? "☒" : "□";
        var pBox = slot.InspectionType == "P" ? "☒" : "□";
        return $"{lBox} L  {pBox} P  ({slot.LastInspectionMonthYear})";
    }

    private static string AssistMarker(AssistType assistType) => assistType switch
    {
        AssistType.FullAssist => "Full assist",
        AssistType.HalfAssist => "Halv assist",
        AssistType.SelfLoading => "Själv lastn",
        _ => string.Empty,
    };

    /// <summary>
    /// Sätter in <paramref name="value"/> direkt efter <paramref name="label"/>.
    /// Mallens etikett följs av en lång rad mellanslag fram till nästa etikett
    /// (plats för handskriven text) - vi "äter" av det mellanrummet med lika
    /// många tecken som vi sätter in, så att en eventuell efterföljande
    /// etikett (t.ex. "Åkeri:") stannar kvar på sin ursprungliga position
    /// istället för att tryckas åt höger.
    /// </summary>
    private static string ReplaceAfterLabel(string text, string label, string value)
    {
        var idx = text.IndexOf(label, StringComparison.Ordinal);
        if (idx < 0) return text;

        var insertAt = idx + label.Length;

        var gapLength = 0;
        while (insertAt + gapLength < text.Length && text[insertAt + gapLength] == ' ')
        {
            gapLength++;
        }

        var consumed = Math.Min(value.Length, gapLength);
        var remainingGap = gapLength - consumed;
        return text[..insertAt] + value + new string(' ', remainingGap) + text[(insertAt + gapLength)..];
    }

    /// <summary>Hittar den "□" som står närmast före <paramref name="marker"/> och kryssar i den (☒).</summary>
    private static string TickCheckbox(string text, string marker)
    {
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return text;

        var boxIndex = text.LastIndexOf('□', markerIndex);
        if (boxIndex < 0) return text;

        var chars = text.ToCharArray();
        chars[boxIndex] = '☒';
        return new string(chars);
    }

    /// <summary>Kryssar i den första markören som hittas i texten, i angiven prioritetsordning.</summary>
    private static string TickFirstMatch(string text, params string[] markers)
    {
        foreach (var marker in markers)
        {
            if (text.Contains(marker, StringComparison.Ordinal))
            {
                return TickCheckbox(text, marker);
            }
        }

        return text;
    }

    /// <summary>
    /// Kryssar i rätt UN-nummerruta/-rutor (t.ex. "□ UN 2582") utifrån vilka
    /// UN-nummer som faktiskt valts, oavsett vilka rutor just den mallen råkar ha.
    /// </summary>
    private static string TickUnNumberCheckboxes(string text, IReadOnlyCollection<ProductItem> products)
    {
        var selectedNumbers = products
            .Select(p => Regex.Match(p.UnNumber, @"\d+").Value)
            .Where(n => n.Length > 0)
            .ToHashSet();

        var chars = text.ToCharArray();
        foreach (Match m in Regex.Matches(text, @"□\s*UN\s*(\d+)"))
        {
            if (selectedNumbers.Contains(m.Groups[1].Value))
            {
                chars[m.Index] = '☒'; // "□" är alltid första tecknet i träffen.
            }
        }

        return new string(chars);
    }

    /// <summary>
    /// Kryssar i rätt produktfamilj(er) i "Produkt namn"-raden och fyller i
    /// ev. variantnummer (t.ex. "111" för PIX 111) i mallens tomrum.
    /// </summary>
    private static string TickProductCheckboxes(string text, IReadOnlyCollection<ProductItem> products)
    {
        // Saltsyra (SAS) - ingen variant att fylla i.
        if (products.Any(p => p.Family == "SAS"))
        {
            text = TickCheckbox(text, "Saltsyra");
        }

        // PIX / PAX / BDP - kryssa och fyll i vald(a) variant(er), t.ex. "111".
        text = TickFamilyWithVariant(text, products, "PIX");
        text = TickFamilyWithVariant(text, products, "PAX");
        text = TickFamilyWithVariant(text, products, "BDP");

        // AKD / Fennosize (Typ 2-mallen).
        var akd = products.FirstOrDefault(p => p.Family == "AKD");
        if (akd is not null)
        {
            text = TickCheckbox(text, "AKD");
            var kdIndex = akd.Name.IndexOf("KD ", StringComparison.OrdinalIgnoreCase);
            var variant = kdIndex >= 0 ? akd.Name[(kdIndex + 3)..].Trim() : string.Empty;
            text = FillVariantBlank(text, "KD", variant);
        }

        // Svavelsyra (Typ 2-mallen) - 94-97% och 98% har egna kryssrutor,
        // 37% saknar en ruta i mallen och lämnas därför okryssad.
        if (products.Any(p => p.Family == "SVS" && (p.Name.Contains("94") || p.Name.Contains("97"))))
        {
            text = TickCheckbox(text, "Svavelsyra (94%-97%)");
        }

        if (products.Any(p => p.Family == "SVS" && p.Name.Contains("98")))
        {
            text = TickCheckbox(text, "Svavelsyra (98%)");
        }

        return text;
    }

    private static string TickFamilyWithVariant(string text, IReadOnlyCollection<ProductItem> products, string family)
    {
        var matches = products.Where(p => p.Family == family).ToList();
        if (matches.Count == 0) return text;

        text = TickCheckbox(text, family);

        var variants = matches
            .Select(p => p.Name.Length > family.Length ? p.Name[family.Length..].Trim() : string.Empty)
            .Where(v => v.Length > 0)
            .Distinct();
        text = FillVariantBlank(text, family, string.Join(", ", variants));

        return text;
    }

    /// <summary>Ersätter en understreckad "tomrumsrad" (t.ex. "PIX ________") med en faktisk variantsträng.</summary>
    private static string FillVariantBlank(string text, string anchor, string variant)
    {
        if (string.IsNullOrWhiteSpace(variant)) return text;

        var pattern = $@"({Regex.Escape(anchor)}\s*)_+";
        return Regex.Replace(text, pattern, m => m.Groups[1].Value + variant);
    }
}

using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DriverChecklist.Api.Models;
using DriverChecklist.Api.Models.Enums;
using Drawing = DocumentFormat.OpenXml.Drawing;
using SpreadsheetDrawing = DocumentFormat.OpenXml.Drawing.Spreadsheet;

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
        FillSignature(sheet);
        FillFirstPage(sheet, request);

        var outputStream = new MemoryStream();
        workbook.SaveAs(outputStream);
        templateStream.Position = 0;
        PreserveDrawingsAndAddNames(templateStream, outputStream, request);
        outputStream.Position = 0;

        var fileName = $"Checklista_{request.Truck.RegNr}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return new ChecklistFile(
            outputStream,
            fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public ChecklistPageResponse? GetFirstPage(ChecklistTemplate templateType)
    {
        var path = _templateResolver.ResolveTemplatePath(templateType);
        if (path is null || !File.Exists(path)) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        return ReadFirstPage(workbook.Worksheet(1));
    }

    private static ChecklistPageResponse ReadFirstPage(IXLWorksheet sheet)
    {
        var rows = new List<ChecklistRowResponse>();
        var sections = new List<ChecklistSectionResponse>();
        for (var row = 13; row <= 30; row++)
        {
            var text = sheet.Cell(row, 1).GetString().Trim();
            if (text.Length == 0) continue;
            if (text.Contains('□'))
            {
                sections.Add(new(row, text.Split('\n')[0].Trim(),
                    Regex.Matches(text, @"□\s*([^□\r\n]+)").Select(m => m.Groups[1].Value.Trim()).ToList()));
                continue;
            }
            var questionEnd = text.IndexOf('?');
            var instructionStart = questionEnd >= 0 && questionEnd + 1 < text.Length ? questionEnd + 1 : -1;
            var enabled = Enumerable.Range(2, 3).Select(column =>
            {
                var color = sheet.Cell(row, column).Style.Fill.BackgroundColor;
                return color.ColorType != XLColorType.Theme || color.ThemeTint >= 0;
            }).ToList();
            rows.Add(new(row,
                instructionStart < 0 ? text : text[..instructionStart].Trim(),
                instructionStart < 0 ? "" : text[instructionStart..].Trim(),
                new[] { 15, 16, 18, 19, 25 }.Contains(row) ? "" : sheet.Cell(row, 5).GetString(),
                enabled));
        }
        return new(rows, sections,
            Regex.Matches(sheet.Cell("A8").GetString(), @"UN\s*(\d+)").Select(m => $"UN {m.Groups[1].Value}").ToList(),
            Regex.Matches(sheet.Cell("E16").GetString(), @"□\s*([^□\r\n]+)").Select(m => m.Groups[1].Value.Trim()).ToList());
    }

    public string? Validate(GenerateChecklistRequest request)
    {
        if (request.Truck is null || request.Trailers is null || request.TankSlots is null || request.SelectedProducts is null)
            return "Transportuppgifterna är ofullständiga.";
        if (request.TankSlots.Count > 4) return "Checklistan har plats för högst fyra tankar.";
        if (request.FirstPage is not { } page) return null;
        if (page.Rows is null || page.Roles is null || page.UnNumbers is null || page.CompartmentVolumes is null ||
            page.Rows.Any(r => r is null) || page.Roles.Any(r => r is null || r.Selected is null))
            return "Uppgifterna på checklistans första sida är ofullständiga.";
        var definition = GetFirstPage(request.TemplateType);
        if (definition is null) return null; // Generation returns the existing template-not-found response.
        if (page.CompartmentVolumes.Count > 6) return "Checklistan har plats för högst sex fackvolymer.";
        if (page.Rows.Select(r => r.Row).Distinct().Count() != page.Rows.Count ||
            page.Rows.Any(r => !definition.Rows.Any(d => d.Row == r.Row)))
            return "Ogiltiga kontrollrader på checklistans första sida.";
        foreach (var row in page.Rows)
        {
            var enabled = definition.Rows.Single(d => d.Row == row.Row).Enabled;
            if ((!enabled[0] && row.Tt) || (!enabled[1] && row.Tc) || (!enabled[2] && row.Rc))
                return "En gråmarkerad kontrollruta kan inte kryssas i.";
        }
        if (page.Roles.Select(r => r.Row).Distinct().Count() != page.Roles.Count ||
            page.Roles.Any(r => !definition.Sections.Any(d => d.Row == r.Row &&
                r.Selected.All(s => d.Roles.Contains(s)))))
            return "Ogiltig roll på checklistans första sida.";
        if (request.IsNewDriver && page.Roles.Any(r => r.Selected.Any(s =>
            s.Contains("Själv lastn", StringComparison.Ordinal) &&
            !(s.Contains("Halv assist", StringComparison.Ordinal) && request.AssistType == AssistType.HalfAssist))))
            return "En ny chaufför kan inte markeras som godkänd självlastare.";
        if (page.UnNumbers.Any(n => !definition.UnNumbers.Contains(n)))
            return "UN-numret finns inte i den valda checklistemallen.";
        return null;
    }

    private static void FillHeaderAndStatus(IXLWorksheet sheet, GenerateChecklistRequest req)
    {
        // A5: "Datum/Tid: ... Åkeri: ..." - sätt in värdena direkt efter
        // respektive etikett så att mallens egen mellanrumsbredd bevaras
        // (annars hamnar "Åkeri:" fel positionerat, precis som tidigare).
        var a5 = sheet.Cell("A5").GetString();
        var timestamp = req.FirstPage?.Timestamp?.ToLocalTime() ?? DateTimeOffset.Now;
        a5 = ReplaceAfterLabel(a5, "Datum/Tid:", $" {timestamp:yyyy-MM-dd HH:mm}");
        a5 = ReplaceAfterLabel(a5, "Åkeri:", $" {req.Akeri}");
        sheet.Cell("A5").Value = a5;

        // A6: "Chaufförens namn: ... Reg nr. Bil-Släp: ..." - samma princip.
        var regSummary = req.Truck.RegNr +
            (req.Trailers.Count > 0 ? " - " + string.Join("/", req.Trailers.Select(t => t.RegNr)) : "");
        var a6 = sheet.Cell("A6").GetString();
        a6 = ReplaceAfterLabel(a6, "Chaufförens namn:", $" {req.DriverName}");
        a6 = ReplaceAfterLabel(a6, "Reg nr. Bil-Släp:", $" {regSummary}");
        sheet.Cell("A6").Value = a6;

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
        if (req.IsNewDriver) sheet.Cell("E15").GetRichText().AddText(" NY CHAUFFÖR").SetBold();

        var approval = sheet.Cell("E17").CreateRichText();
        var vehicles = new[] { req.Truck }.Concat(req.Trailers).ToList();
        for (var i = 0; i < vehicles.Count; i++)
        {
            if (i > 0) approval.AddText("\n");
            var vehicle = vehicles[i];
            approval.AddText($"{(i == 0 ? "Bil" : $"Släp {i}")}: {vehicle.ApprovalExpiry ?? "-"}");
            if (vehicle.IsNew) approval.AddText(i == 0 ? " NY BIL" : " NY SLÄP").SetBold();
        }

        // E16: Kryssa i vald lastningsassistans (redigerbar i Visa/Redigera-modalen).
        // Självlastning kräver en känd chaufför, även vid ett manuellt assistansval.
        var assistType = EffectiveAssistType(req);
        if (assistType != AssistType.Unspecified)
        {
            var e16 = sheet.Cell("E16").GetString();
            sheet.Cell("E16").Value = TickCheckbox(e16, AssistMarker(assistType));
        }

        sheet.Cell("E18").Value =
            "ADR/RID Tank kod el UN mobil Tank Instruktion:\n\n" +
            $"Tank 1:  {req.TankSlots.ElementAtOrDefault(0)?.TankCode ?? "-",-12} Tank 2: {req.TankSlots.ElementAtOrDefault(1)?.TankCode ?? "-",-12}\n\n" +
            $"Tank 3:  {req.TankSlots.ElementAtOrDefault(2)?.TankCode ?? "-",-12} Tank 4: {req.TankSlots.ElementAtOrDefault(3)?.TankCode ?? "-",-12}";
        sheet.Cell("E19").Value =
            "Inspektion Typ & (mån/år)\n" +
            $"Tank 1:  {FormatInspection(req.TankSlots.ElementAtOrDefault(0))}\n" +
            $"Tank 2:  {FormatInspection(req.TankSlots.ElementAtOrDefault(1))}\n" +
            $"Tank 3:  {FormatInspection(req.TankSlots.ElementAtOrDefault(2))}\n" +
            $"Tank 4:  {FormatInspection(req.TankSlots.ElementAtOrDefault(3))}";
    }

    private static void FillFirstPage(IXLWorksheet sheet, GenerateChecklistRequest req)
    {
        if (req.FirstPage is not { } page) return;
        var a7 = ReplaceAfterLabel(sheet.Cell("A7").GetString(), "Kemira SAP Nr:", $" {page.SapNumber}");
        sheet.Cell("A7").Value = ReplaceAfterLabel(a7, "Preliminär Lastnings Mängd (ton):", $" {page.LoadingAmount}");
        var a8 = ReplaceAfterLabel(sheet.Cell("A8").GetString(), "Container/Järnvägsvagns nr:", $" {page.ContainerNumber}");
        a8 = a8.Replace('☒', '□');
        foreach (var number in page.UnNumbers) a8 = TickCheckbox(a8, number);
        sheet.Cell("A8").Value = a8;

        foreach (var role in page.Roles)
        {
            var text = sheet.Cell(role.Row, 1).GetString().Replace('☒', '□');
            foreach (var selected in role.Selected) text = TickCheckbox(text, selected);
            sheet.Cell(role.Row, 1).Value = text;
        }
        if (page.CompartmentVolumes.Count > 0)
        {
            sheet.Cell("E25").Value = "Tank Volym i Liter (L)\n\n" +
                string.Join("\n\n", Enumerable.Range(0, 3).Select(i =>
                    $"Fack {i * 2 + 1}: {page.CompartmentVolumes.ElementAtOrDefault(i * 2) ?? ""}    " +
                    $"Fack {i * 2 + 2}: {page.CompartmentVolumes.ElementAtOrDefault(i * 2 + 1) ?? ""}"));
        }
        foreach (var row in page.Rows)
        {
            var checks = new[] { row.Tt, row.Tc, row.Rc };
            for (var i = 0; i < checks.Length; i++)
            {
                var cell = sheet.Cell(row.Row, i + 2);
                cell.Value = checks[i] ? "X" : "";
                cell.Style.Font.FontSize = 24;
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
            if (!string.IsNullOrWhiteSpace(row.Comment))
            {
                var cell = sheet.Cell(row.Row, 5);
                cell.GetRichText().AddText((cell.GetString().Length > 0 ? "\n" : "") + row.Comment);
            }
        }
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
            var updated = EffectiveAssistType(req) switch
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

    private static void FillSignature(IXLWorksheet sheet)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        for (var row = 46; row <= 48; row++)
        {
            var cell = sheet.Cell(row, 2);
            cell.Value = today;
            cell.Style.Font.FontSize = 14;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = false;
        }
    }

    private static AssistType EffectiveAssistType(GenerateChecklistRequest req) =>
        req.IsNewDriver && req.AssistType == AssistType.SelfLoading
            ? AssistType.Unspecified
            : req.AssistType;

    private static void PreserveDrawingsAndAddNames(
        Stream templateStream, Stream outputStream, GenerateChecklistRequest req)
    {
        using var template = SpreadsheetDocument.Open(templateStream, false);
        using var output = SpreadsheetDocument.Open(outputStream, true);
        var templateWorkbook = template.WorkbookPart!;
        var outputWorkbook = output.WorkbookPart!;
        var templateSheet = templateWorkbook.Workbook.Sheets!.Elements<Sheet>().First();
        var outputSheet = outputWorkbook.Workbook.Sheets!.Elements<Sheet>().First();
        var templatePart = (WorksheetPart)templateWorkbook.GetPartById(templateSheet.Id!);
        var outputPart = (WorksheetPart)outputWorkbook.GetPartById(outputSheet.Id!);

        // ClosedXML tappar bildrotationer och textformer. Kopiera hela ritdelen,
        // inklusive bildrelationer, innan de separata namnfälten läggs till.
        if (outputPart.DrawingsPart is { } oldDrawings)
        {
            outputPart.DeletePart(oldDrawings);
        }
        outputPart.Worksheet.RemoveAllChildren<DocumentFormat.OpenXml.Spreadsheet.Drawing>();

        var drawings = templatePart.DrawingsPart is { } originalDrawings
            ? outputPart.AddPart(originalDrawings)
            : outputPart.AddNewPart<DrawingsPart>();
        drawings.WorksheetDrawing ??= new SpreadsheetDrawing.WorksheetDrawing();
        outputPart.Worksheet.AddChild(new DocumentFormat.OpenXml.Spreadsheet.Drawing
        {
            Id = outputPart.GetIdOfPart(drawings),
        });

        var nextId = drawings.WorksheetDrawing
            .Descendants<SpreadsheetDrawing.NonVisualDrawingProperties>()
            .Select(properties => properties.Id?.Value ?? 0U)
            .DefaultIfEmpty(0U).Max() + 1;
        AddName(drawings.WorksheetDrawing, 46, req.OperatorName, nextId);
        var row47Name = req.AssistType == AssistType.FullAssist ? req.OperatorName : req.DriverName;
        AddName(drawings.WorksheetDrawing, 47, row47Name, nextId + 1);
        drawings.WorksheetDrawing.Save();
        outputPart.Worksheet.Save();
    }

    private static void AddName(
        SpreadsheetDrawing.WorksheetDrawing drawings, int row, string name, uint id)
    {
        var shape = new SpreadsheetDrawing.Shape(
            new SpreadsheetDrawing.NonVisualShapeProperties(
                new SpreadsheetDrawing.NonVisualDrawingProperties
                {
                    Id = id,
                    Name = $"ChecklistName{row}",
                },
                new SpreadsheetDrawing.NonVisualShapeDrawingProperties { TextBox = true }),
            new SpreadsheetDrawing.ShapeProperties(
                new Drawing.PresetGeometry(new Drawing.AdjustValueList())
                {
                    Preset = Drawing.ShapeTypeValues.Rectangle,
                },
                new Drawing.NoFill(),
                new Drawing.Outline(new Drawing.NoFill())),
            new SpreadsheetDrawing.TextBody(
                new Drawing.BodyProperties
                {
                    Anchor = Drawing.TextAnchoringTypeValues.Bottom,
                    LeftInset = 38100,
                    RightInset = 38100,
                    TopInset = 38100,
                    BottomInset = 38100,
                    Wrap = Drawing.TextWrappingValues.Square,
                },
                new Drawing.ListStyle(),
                new Drawing.Paragraph(
                    new Drawing.ParagraphProperties { Alignment = Drawing.TextAlignmentTypeValues.Right },
                    new Drawing.Run(
                        new Drawing.RunProperties(
                            new Drawing.SolidFill(new Drawing.RgbColorModelHex { Val = "000000" }),
                            new Drawing.LatinFont { Typeface = "Arial" })
                        {
                            FontSize = 1800,
                        },
                        new Drawing.Text(name)))));

        drawings.Append(new SpreadsheetDrawing.TwoCellAnchor(
            new SpreadsheetDrawing.FromMarker(
                new SpreadsheetDrawing.ColumnId("0"),
                new SpreadsheetDrawing.ColumnOffset("0"),
                new SpreadsheetDrawing.RowId((row - 1).ToString()),
                new SpreadsheetDrawing.RowOffset("0")),
            new SpreadsheetDrawing.ToMarker(
                new SpreadsheetDrawing.ColumnId("1"),
                new SpreadsheetDrawing.ColumnOffset("0"),
                new SpreadsheetDrawing.RowId(row.ToString()),
                new SpreadsheetDrawing.RowOffset("0")),
            shape,
            new SpreadsheetDrawing.ClientData())
        {
            EditAs = SpreadsheetDrawing.EditAsValues.TwoCell,
        });
    }

    private static string FormatInspection(TankSlot? slot)
    {
        if (slot is null)
        {
            return "□ L  □ P  (      /      )";
        }

        var lBox = slot.InspectionType == "L" ? "☒" : "□";
        var pBox = slot.InspectionType == "P" ? "☒" : "□";
        return $"{lBox} L  {pBox} P  ({(string.IsNullOrWhiteSpace(slot.LastInspectionMonthYear) ? "      /      " : slot.LastInspectionMonthYear)})";
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

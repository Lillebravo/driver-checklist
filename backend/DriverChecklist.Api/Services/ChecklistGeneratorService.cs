using ClosedXML.Excel;
using DriverChecklist.Api.Models;

namespace DriverChecklist.Api.Services;

/// <summary>
/// MVP-implementationen av checklistegenerering via ClosedXML. Mallen öppnas
/// alltid read-only med delad åtkomst (se README.md avsnitt 7) så att andra
/// användare kan ha filen öppen samtidigt i Teams/SharePoint - inga ändringar
/// skrivs någonsin tillbaka till mallfilen på disk.
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
        // A5: Datum/Tid: ... Åkeri: ...
        sheet.Cell("A5").Value = $"Datum/Tid: {DateTime.Now:yyyy-MM-dd HH:mm}           Åkeri: {req.Akeri ?? ""}";

        // A6: Chaufförens namn: ... Reg nr. Bil-Släp: ...
        var regSummary = req.Truck.RegNr +
            (req.Trailers.Count > 0 ? " - " + string.Join("/", req.Trailers.Select(t => t.RegNr)) : "");
        sheet.Cell("A6").Value = $"Chaufförens namn: {req.DriverName,-35} Reg nr. Bil-Släp: {regSummary}";

        // A8 & A9: UN-nummer och Produktnamn
        sheet.Cell("A8").Value =
            $"Container/Järnvägsvagns nr:                     UN Nummer: {string.Join(", ", req.SelectedProducts.Select(p => p.UnNumber).Distinct())}";
        sheet.Cell("A9").Value = $"Produkt namn: {string.Join(", ", req.SelectedProducts.Select(p => p.Name))}";

        // Statusflaggor för nya aktörer
        var flags = new List<string>();
        if (req.IsNewDriver) flags.Add("[NY CHAUFFÖR]");
        if (req.Truck.IsNew) flags.Add("[NY DRAGBIL]");
        for (var i = 0; i < req.Trailers.Count; i++)
        {
            if (req.Trailers[i].IsNew) flags.Add($"[NYTT SLÄP {i + 1}]");
        }

        if (flags.Count > 0)
        {
            sheet.Cell("A7").Value =
                $"Kemira SAP Nr:                                Preliminär Mängd:            STATUS: {string.Join(" ", flags)}";
        }
    }

    private static void FillCheckIn(IXLWorksheet sheet, GenerateChecklistRequest req)
    {
        // A13: Markera [X] Vakt
        sheet.Cell("A13").Value = "Check in \n□ Fordonskontrollant  ☒ Vakt  □ Exp";

        // B14 t.o.m B19 (TT-kolumnen): Klicka i "Ja"
        for (var r = 14; r <= 19; r++)
        {
            sheet.Cell(r, 2).Value = "Ja"; // Kolumn B = TT Ja
        }

        // E15: Chaufförens ADR-giltighet
        sheet.Cell("E15").Value = $"Giltighet: {req.DriverAdrExpiry:yyyy-MM-dd}";

        // E16: Klicka i Självlastande Helassist
        sheet.Cell("E16").Value =
            "  ☒ Full assist   □ Halv assist\n  □ Själv lastn\n  (Ej godkänd självlastare ska lastas av Operatör)";

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

    private static void FillSignature(IXLWorksheet sheet, GenerateChecklistRequest req)
    {
        // A46: Kemira Fordonskontrollant / Exp / Vakten
        sheet.Cell("A46").Value = $"Vakt: {req.OperatorName} (Kemira Fordonskontrollant/Exp/Vakten)";
        sheet.Cell("B46").Value = DateTime.Now.ToString("yyyy-MM-dd");
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
}

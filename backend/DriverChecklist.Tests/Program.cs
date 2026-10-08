using System.Security.Cryptography;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using DriverChecklist.Api.Configuration;
using DriverChecklist.Api.Models;
using DriverChecklist.Api.Models.Enums;
using DriverChecklist.Api.Services;
using Microsoft.Extensions.Options;
using Drawing = DocumentFormat.OpenXml.Drawing;
using SpreadsheetDrawing = DocumentFormat.OpenXml.Drawing.Spreadsheet;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run --project DriverChecklist.Tests -- <template-directory>");
    return 1;
}

var resolver = new TemplateResolver(Options.Create(new TemplateOptions { Path = Path.GetFullPath(args[0]) }));
var generator = new ChecklistGeneratorService(resolver);
var count = 0;

foreach (var templateType in new[] { ChecklistTemplate.Type1_PixPaxSasBdp, ChecklistTemplate.Type2_SvsAkd })
{
    var path = resolver.ResolveTemplatePath(templateType)!;
    var originalHash = SHA256.HashData(File.ReadAllBytes(path));
    using var originalWorkbook = new XLWorkbook(path);
    using var originalDocument = SpreadsheetDocument.Open(path, false);
    var originalSheet = originalWorkbook.Worksheet(1);
    var originalPart = FirstWorksheet(originalDocument);
    var originalDrawings = originalPart.DrawingsPart
        ?? throw new InvalidOperationException("Template has no drawing part.");
    var originalAnchors = originalDrawings.WorksheetDrawing!.ChildElements
        .Select(element => element.OuterXml).ToArray();
    var originalImages = ImageHashes(originalDrawings);

    foreach (var isNew in new[] { false, true })
    foreach (var assist in Enum.GetValues<AssistType>())
    {
        var request = new GenerateChecklistRequest(
            templateType, "Testvakt", "Testchaufför", new DateTime(2028, 1, 31), isNew,
            "Teståkeri", new VehicleUnit("TEST123", false), [], [], [], assist);
        var result = generator.Generate(request)
            ?? throw new InvalidOperationException("Generation returned no file.");
        using var content = result.Content;
        using var generatedWorkbook = new XLWorkbook(content);
        var generatedSheet = generatedWorkbook.Worksheet(1);

        for (var row = 46; row <= 48; row++)
        {
            Assert(generatedSheet.Cell(row, 1).GetString() == originalSheet.Cell(row, 1).GetString(),
                $"A{row}: instruction text must stay unchanged.");
            Assert(generatedSheet.Cell(row, 1).Style.Alignment.Equals(originalSheet.Cell(row, 1).Style.Alignment),
                $"A{row}: instruction alignment must stay unchanged.");
            var dateCell = generatedSheet.Cell(row, 2);
            Assert(dateCell.GetString() == DateTime.Now.ToString("yyyy-MM-dd"), $"B{row}: date missing.");
            Assert(dateCell.Style.Font.FontSize == 14, $"B{row}: date must use 14 pt.");
            Assert(dateCell.Style.Alignment.Horizontal == XLAlignmentHorizontalValues.Center
                && dateCell.Style.Alignment.Vertical == XLAlignmentVerticalValues.Center
                && !dateCell.Style.Alignment.WrapText, $"B{row}: date alignment or wrapping incorrect.");
            using var measurement = new XLWorkbook();
            var measureSheet = measurement.AddWorksheet("DateWidth");
            measureSheet.Cell("A1").Value = dateCell.Value;
            measureSheet.Cell("A1").Style = dateCell.Style;
            measureSheet.Column(1).AdjustToContents();
            Assert(measureSheet.Column(1).Width <= generatedSheet.Column(2).Width + generatedSheet.Column(3).Width,
                $"B{row}: enlarged date width {measureSheet.Column(1).Width} exceeds box width "
                + $"{generatedSheet.Column(2).Width + generatedSheet.Column(3).Width}.");
        }

        var shouldTick = assist != AssistType.Unspecified && !(isNew && assist == AssistType.SelfLoading);
        foreach (var address in new[] { "E16", "A21", "A28", "A33" })
        {
            var originalText = originalSheet.Cell(address).GetString().ReplaceLineEndings("\n");
            var marker = assist switch
            {
                AssistType.FullAssist => "Full assist",
                AssistType.HalfAssist => "Halv assist",
                AssistType.SelfLoading => "Själv",
                _ => "",
            };
            var hasCheckbox = marker.Length > 0 && originalText.Contains(marker, StringComparison.Ordinal);
            var expectedText = originalText.ToCharArray();
            if (shouldTick && hasCheckbox)
            {
                var markerIndex = originalText.IndexOf(marker, StringComparison.Ordinal);
                var boxIndex = originalText.LastIndexOf('□', markerIndex);
                Assert(boxIndex >= 0, $"{address}: template marker has no checkbox.");
                expectedText[boxIndex] = '☒';
            }
            Assert(generatedSheet.Cell(address).GetString().ReplaceLineEndings("\n") == new string(expectedText),
                $"{address}: incorrect checkbox for {assist}, new={isNew}. "
                + $"Expected: {new string(expectedText).Replace("\r", "\\r").Replace("\n", "\\n")} "
                + $"Actual: {generatedSheet.Cell(address).GetString().Replace("\r", "\\r").Replace("\n", "\\n")}");
        }

        content.Position = 0;
        using var generatedDocument = SpreadsheetDocument.Open(content, false);
        var generatedPart = FirstWorksheet(generatedDocument);
        var generatedDrawings = generatedPart.DrawingsPart
            ?? throw new InvalidOperationException("Generated drawing part missing.");
        var drawing = generatedDrawings.WorksheetDrawing!;
        Assert(originalAnchors.SequenceEqual(drawing.ChildElements.Take(originalAnchors.Length)
            .Select(element => element.OuterXml)), "Template drawings/rotation changed.");
        Assert(originalImages.SequenceEqual(ImageHashes(generatedDrawings)), "Template images changed.");
        var drawingReference = generatedPart.Worksheet.Elements<DocumentFormat.OpenXml.Spreadsheet.Drawing>().Single();
        Assert(generatedPart.GetPartById(drawingReference.Id!) == generatedDrawings, "Drawing relationship broken.");

        var names = drawing.Elements<SpreadsheetDrawing.TwoCellAnchor>()
            .Where(anchor => anchor.Descendants<SpreadsheetDrawing.NonVisualDrawingProperties>()
                .Any(properties => properties.Name?.Value?.StartsWith("ChecklistName", StringComparison.Ordinal) == true))
            .ToArray();
        Assert(names.Length == 2, "Exactly two separate name fields expected.");
        for (var index = 0; index < names.Length; index++)
        {
            var anchor = names[index];
            var expectedName = index == 0 || assist == AssistType.FullAssist ? request.OperatorName : request.DriverName;
            Assert(anchor.Descendants<Drawing.Text>().Single().Text == expectedName, "Name field contains wrong text.");
            Assert(anchor.Descendants<Drawing.BodyProperties>().Single().Anchor?.Value == Drawing.TextAnchoringTypeValues.Bottom,
                "Name must be bottom aligned.");
            Assert(anchor.Descendants<Drawing.ParagraphProperties>().Single().Alignment?.Value == Drawing.TextAlignmentTypeValues.Right,
                "Only name must be right aligned.");
            Assert(anchor.FromMarker!.ColumnId!.Text == "0" && anchor.ToMarker!.ColumnId!.Text == "1"
                && anchor.FromMarker.RowId!.Text == (45 + index).ToString()
                && anchor.ToMarker.RowId!.Text == (46 + index).ToString(), "Name must stay inside its box.");
            var errors = new OpenXmlValidator().Validate(anchor).ToArray();
            Assert(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.Description)));
        }
        Assert(originalSheet.MergedRanges.Select(range => range.RangeAddress.ToString())
            .SequenceEqual(generatedSheet.MergedRanges.Select(range => range.RangeAddress.ToString())),
            "Merged boxes changed.");
        count++;
    }
    Assert(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "Template modified on disk.");
}

Console.WriteLine($"PASS: {count} generated workbooks; drawings, names, dates, checkboxes and unchanged templates verified.");
return 0;

static WorksheetPart FirstWorksheet(SpreadsheetDocument document)
{
    var workbook = document.WorkbookPart!;
    var sheet = workbook.Workbook.Sheets!.Elements<Sheet>().First();
    return (WorksheetPart)workbook.GetPartById(sheet.Id!);
}

static string[] ImageHashes(DrawingsPart part) => part.ImageParts.Select(image =>
{
    using var stream = image.GetStream();
    return Convert.ToHexString(SHA256.HashData(stream));
}).Order(StringComparer.Ordinal).ToArray();

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

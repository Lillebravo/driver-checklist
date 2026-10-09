using DriverChecklist.Api.Configuration;
using DriverChecklist.Api.Models.Enums;
using Microsoft.Extensions.Options;

namespace DriverChecklist.Api.Services;

/// <summary>
/// Slår upp mallfilsnamn per checklistetyp och kombinerar med den konfigurerade
/// mallmappen (<see cref="TemplateOptions.Path"/>).
/// </summary>
public class TemplateResolver : ITemplateResolver
{
    private static readonly Dictionary<ChecklistTemplate, string> FileNamesByTemplate = new()
    {
        [ChecklistTemplate.Type1_PixPaxSasBdp] = "Ny 1 Saltsyra , Pix, mm. Tankar MED skyddande beläggning.xlsx",
        [ChecklistTemplate.Type2_SvsAkd] = "Ny 2 Svavelsyra 94,97 och 98 Fennosize. Tankar UTAN skyddande beläggning.xlsx",
        // Typ 3 (ALS/LUT) saknar ännu en mallfil - läggs till här när den finns.
        [ChecklistTemplate.Type3_AlsLut] = "Mall_Checklista_Typ3.xlsx",
    };

    private readonly TemplateOptions _options;

    public TemplateResolver(IOptions<TemplateOptions> options)
    {
        _options = options.Value;
    }

    public string? ResolveTemplatePath(ChecklistTemplate templateType)
    {
        if (!FileNamesByTemplate.TryGetValue(templateType, out var fileName))
        {
            return null;
        }

        var folder = string.IsNullOrWhiteSpace(_options.Path)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : _options.Path;
        if (!Path.IsPathFullyQualified(folder))
            folder = Path.GetFullPath(folder, AppContext.BaseDirectory);

        return Path.Combine(folder, fileName);
    }
}

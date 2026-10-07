using DriverChecklist.Api.Models.Enums;

namespace DriverChecklist.Api.Services;

/// <summary>
/// Slår upp den fysiska sökvägen till rätt Excel-mall för en given checklistetyp.
/// </summary>
public interface ITemplateResolver
{
    /// <summary>
    /// Returnerar fulla sökvägen till mallfilen, eller null om typen saknar en
    /// känd mallfil (t.ex. Typ 3 som ännu inte har en mall i MVP:n).
    /// </summary>
    string? ResolveTemplatePath(ChecklistTemplate templateType);
}

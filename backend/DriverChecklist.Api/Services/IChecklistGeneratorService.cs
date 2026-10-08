using DriverChecklist.Api.Models;

namespace DriverChecklist.Api.Services;

/// <summary>
/// Fyller i en checklistemall med data för en transport och returnerar den
/// som en nedladdningsbar Excel-fil.
/// </summary>
public interface IChecklistGeneratorService
{
    /// <summary>Reads editable first-page controls without changing the template.</summary>
    ChecklistPageResponse? GetFirstPage(Models.Enums.ChecklistTemplate templateType);
    /// <summary>Returns a user-facing error when submitted controls cannot be exported.</summary>
    string? Validate(GenerateChecklistRequest request);
    /// <summary>
    /// Genererar en checklista. Returnerar null om mallfilen för den begärda
    /// typen inte kunde hittas på disk.
    /// </summary>
    ChecklistFile? Generate(GenerateChecklistRequest request);
}

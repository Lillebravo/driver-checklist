using DriverChecklist.Api.Models;

namespace DriverChecklist.Api.Services;

/// <summary>
/// Fyller i en checklistemall med data för en transport och returnerar den
/// som en nedladdningsbar Excel-fil.
/// </summary>
public interface IChecklistGeneratorService
{
    /// <summary>
    /// Genererar en checklista. Returnerar null om mallfilen för den begärda
    /// typen inte kunde hittas på disk.
    /// </summary>
    ChecklistFile? Generate(GenerateChecklistRequest request);
}

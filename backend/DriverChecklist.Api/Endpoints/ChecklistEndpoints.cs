using DriverChecklist.Api.Models;
using DriverChecklist.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DriverChecklist.Api.Endpoints;

/// <summary>Endpoints för att generera och ladda ner färdigifyllda checklistor.</summary>
public static class ChecklistEndpoints
{
    public static void MapChecklistEndpoints(this WebApplication app)
    {
        app.MapPost("/api/checklist/generate", (
            [FromBody] GenerateChecklistRequest request,
            IChecklistGeneratorService checklistGeneratorService) =>
        {
            var result = checklistGeneratorService.Generate(request);
            if (result is null)
            {
                return Results.NotFound($"Kunde inte hitta mallfilen för checklistetyp: {request.TemplateType}");
            }

            return Results.File(result.Content, result.ContentType, result.FileName);
        });
    }
}

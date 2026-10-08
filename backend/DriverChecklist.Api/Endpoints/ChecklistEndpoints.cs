using DriverChecklist.Api.Models;
using DriverChecklist.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DriverChecklist.Api.Endpoints;

/// <summary>Endpoints för att generera och ladda ner färdigifyllda checklistor.</summary>
public static class ChecklistEndpoints
{
    public static void MapChecklistEndpoints(this WebApplication app)
    {
        app.MapGet("/api/checklist/first-page/{templateType}", Microsoft.AspNetCore.Http.HttpResults.Results<
            Microsoft.AspNetCore.Http.HttpResults.Ok<ChecklistPageResponse>,
            Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult> (
            Models.Enums.ChecklistTemplate templateType, IChecklistGeneratorService service, CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = service.GetFirstPage(templateType);
            if (page is null)
                return TypedResults.Problem(statusCode: 404, detail: "Kunde inte hitta checklistemallen.");
            return TypedResults.Ok(page);
        }).WithName("GetChecklistFirstPage")
            .WithSummary("Läser första sidans fält och kontrollrader ur Excel-mallen.");

        app.MapPost("/api/checklist/generate", (
            [FromBody] GenerateChecklistRequest request,
            IChecklistGeneratorService checklistGeneratorService) =>
        {
            var validationError = checklistGeneratorService.Validate(request);
            if (validationError is not null) return Results.Problem(statusCode: 400, detail: validationError);
            var result = checklistGeneratorService.Generate(request);
            if (result is null)
            {
                return Results.NotFound($"Kunde inte hitta mallfilen för checklistetyp: {request.TemplateType}");
            }

            return Results.File(result.Content, result.ContentType, result.FileName);
        });
    }
}

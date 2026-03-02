using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Api.Endpoints;

public static class SchemaEndpoints
{
    public static void MapSchemaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/schemas").WithTags("Schemas");

        // GET /api/schemas/{returnCode}
        group.MapGet("/{returnCode}", (
            string returnCode,
            IXsdSchemaProvider schemaProvider) =>
        {
            try
            {
                var rc = ReturnCode.Parse(returnCode);
                if (!schemaProvider.HasSchema(rc))
                    return Results.NotFound(new { error = $"XSD schema not found for '{returnCode}'" });

                var xsdFileName = rc.ToXsdFileName();
                var assembly = typeof(Infrastructure.Xml.XsdSchemaProvider).Assembly;
                var resourceName = $"FC.Engine.Infrastructure.Xml.Schemas.{xsdFileName}";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    return Results.NotFound();

                using var reader = new StreamReader(stream);
                var xsd = reader.ReadToEnd();

                return Results.Content(xsd, "application/xml");
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GetSchema")
        .WithOpenApi();
    }
}

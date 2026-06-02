using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;
using ZOSAPI.Editors.NCE;

namespace ZemaxMCP.Server.Tools.NonSequential;

[McpServerToolType]
public class NscAddObjectTool
{
    private readonly IZemaxSession _session;

    public NscAddObjectTool(IZemaxSession session) => _session = session;

    public record NscAddObjectResult(
        bool Success,
        string? Error,
        int ObjectNumber,
        string Type,
        int TotalObjects
    );

    [McpServerTool(Name = "zemax_nsc_add_object")]
    [Description("Add a new object to the Non-sequential Component Editor (NCE) and set its type. " +
                 "Use a type name from zemax_nsc_list_object_types (e.g. 'Source Point', 'Standard Lens', " +
                 "'Detector Rectangle'). Optionally set position, material, and comment. " +
                 "Use zemax_nsc_set_object afterwards to configure type-specific parameter columns.")]
    public async Task<NscAddObjectResult> ExecuteAsync(
        [Description("Object type name, exactly as listed by zemax_nsc_list_object_types.")] string objectType,
        [Description("Insert position (1-based). 0 appends after the last object.")] int insertAt = 0,
        [Description("X position.")] double x = 0,
        [Description("Y position.")] double y = 0,
        [Description("Z position.")] double z = 0,
        [Description("Material/glass name (empty for default/air; e.g. 'MIRROR' for a reflector).")] string? material = null,
        [Description("Object comment/label.")] string? comment = null)
    {
        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["objectType"] = objectType,
                ["insertAt"] = insertAt,
                ["x"] = x, ["y"] = y, ["z"] = z,
                ["material"] = material,
                ["comment"] = comment
            };

            return await _session.ExecuteAsync("NscAddObject", parameters, system =>
            {
                var nce = system.NCE;

                INCERow row = (insertAt > 0 && insertAt <= nce.NumberOfObjects)
                    ? nce.InsertNewObjectAt(insertAt)
                    : nce.AddObject();

                // Resolve and apply the object type.
                var objType = nce.ObjectTypeFromObjectName(objectType);
                var settings = row.GetObjectTypeSettings(objType);
                if (!settings.IsValid)
                    throw new ArgumentException(
                        $"Unknown or unavailable object type '{objectType}'. " +
                        "Use zemax_nsc_list_object_types to see valid names.");

                if (!row.ChangeType(settings))
                    throw new InvalidOperationException($"Failed to set object type to '{objectType}'.");

                // Apply common properties after the type change (ChangeType can reset the row).
                row.XPosition = x;
                row.YPosition = y;
                row.ZPosition = z;
                if (!string.IsNullOrEmpty(material))
                    row.Material = material;
                if (!string.IsNullOrEmpty(comment))
                    row.Comment = comment;

                return new NscAddObjectResult(
                    Success: true,
                    Error: null,
                    ObjectNumber: row.ObjectNumber,
                    Type: row.TypeName ?? row.Type.ToString(),
                    TotalObjects: nce.NumberOfObjects
                );
            });
        }
        catch (Exception ex)
        {
            return new NscAddObjectResult(false, ex.Message, 0, objectType, 0);
        }
    }
}

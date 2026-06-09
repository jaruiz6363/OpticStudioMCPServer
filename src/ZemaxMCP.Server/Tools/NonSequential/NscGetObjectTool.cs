using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;
using ZOSAPI.Editors.NCE;

namespace ZemaxMCP.Server.Tools.NonSequential;

[McpServerToolType]
public class NscGetObjectTool
{
    private readonly IZemaxSession _session;

    public NscGetObjectTool(IZemaxSession session) => _session = session;

    public record NscGetObjectResult(
        bool Success,
        string? Error,
        int ObjectNumber,
        string Type,
        string Comment,
        double X,
        double Y,
        double Z,
        double TiltX,
        double TiltY,
        double TiltZ,
        string Material,
        int RefObject,
        int InsideOf,
        List<NscParameterInfo> Parameters
    );

    [McpServerTool(Name = "zemax_nsc_get_object")]
    [Description("Get full detail for a single non-sequential object, including its type-specific parameter " +
                 "columns (Par1..ParN) paired with their labels. Use this to discover which parameter index " +
                 "controls a given setting (e.g. number of rays on a source, pixel count on a detector) before " +
                 "calling zemax_nsc_set_object.")]
    public async Task<NscGetObjectResult> ExecuteAsync(
        [Description("Object number in the NCE (1-based).")] int objectNumber)
    {
        try
        {
            var parameters = new Dictionary<string, object?> { ["objectNumber"] = objectNumber };

            return await _session.ExecuteAsync("NscGetObject", parameters, system =>
            {
                var nce = system.NCE;

                if (objectNumber < 1 || objectNumber > nce.NumberOfObjects)
                    throw new ArgumentException(
                        $"Invalid object number: {objectNumber}. Valid range: 1-{nce.NumberOfObjects}.");

                var row = nce.GetObjectAt(objectNumber);

                var paramList = NscCellHelper.ReadParameters(row);

                return new NscGetObjectResult(
                    Success: true,
                    Error: null,
                    ObjectNumber: objectNumber,
                    Type: row.TypeName ?? row.Type.ToString(),
                    Comment: row.Comment ?? "",
                    X: row.XPosition,
                    Y: row.YPosition,
                    Z: row.ZPosition,
                    TiltX: row.TiltAboutX,
                    TiltY: row.TiltAboutY,
                    TiltZ: row.TiltAboutZ,
                    Material: row.Material ?? "",
                    RefObject: row.RefObject,
                    InsideOf: row.InsideOf,
                    Parameters: paramList
                );
            });
        }
        catch (Exception ex)
        {
            return new NscGetObjectResult(false, ex.Message, objectNumber, "", "",
                0, 0, 0, 0, 0, 0, "", 0, 0, new List<NscParameterInfo>());
        }
    }
}

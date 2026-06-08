using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;
using ZOSAPI.Editors.NCE;

namespace ZemaxMCP.Server.Tools.NonSequential;

[McpServerToolType]
public class NscSetObjectTool
{
    private readonly IZemaxSession _session;

    public NscSetObjectTool(IZemaxSession session) => _session = session;

    public record NscSetObjectResult(
        bool Success,
        string? Error,
        NscObjectSummary? UpdatedObject,
        List<NscParameterInfo>? Parameters
    );

    [McpServerTool(Name = "zemax_nsc_set_object")]
    [Description("Modify a non-sequential object: position, tilt, material, comment, reference/inside-of links, " +
                 "and type-specific parameter columns (Par1..ParN). Only supplied fields are changed. " +
                 "Discover parameter indices and labels first with zemax_nsc_get_object. Each parameter is " +
                 "{ index, value } for numeric columns or { index, text } for text columns (e.g. file names).")]
    public async Task<NscSetObjectResult> ExecuteAsync(
        [Description("Object number in the NCE (1-based).")] int objectNumber,
        [Description("X position. Omit to leave unchanged.")] double? x = null,
        [Description("Y position. Omit to leave unchanged.")] double? y = null,
        [Description("Z position. Omit to leave unchanged.")] double? z = null,
        [Description("Tilt about X (degrees). Omit to leave unchanged.")] double? tiltX = null,
        [Description("Tilt about Y (degrees). Omit to leave unchanged.")] double? tiltY = null,
        [Description("Tilt about Z (degrees). Omit to leave unchanged.")] double? tiltZ = null,
        [Description("Material/glass name. Omit to leave unchanged.")] string? material = null,
        [Description("Comment/label. Omit to leave unchanged.")] string? comment = null,
        [Description("Reference object number. Omit to leave unchanged.")] int? refObject = null,
        [Description("'Inside of' object number (for nesting). Omit to leave unchanged.")] int? insideOf = null,
        [Description("Type-specific parameter columns to set. Each is { index (1-based), value } or { index, text }.")]
        List<NscParameter>? parameters = null)
    {
        try
        {
            var logParams = new Dictionary<string, object?>
            {
                ["objectNumber"] = objectNumber,
                ["x"] = x, ["y"] = y, ["z"] = z,
                ["tiltX"] = tiltX, ["tiltY"] = tiltY, ["tiltZ"] = tiltZ,
                ["material"] = material, ["comment"] = comment,
                ["refObject"] = refObject, ["insideOf"] = insideOf,
                ["parameterCount"] = parameters?.Count ?? 0
            };

            return await _session.ExecuteAsync("NscSetObject", logParams, system =>
            {
                var nce = system.NCE;

                if (objectNumber < 1 || objectNumber > nce.NumberOfObjects)
                    throw new ArgumentException(
                        $"Invalid object number: {objectNumber}. Valid range: 1-{nce.NumberOfObjects}.");

                var row = nce.GetObjectAt(objectNumber);

                if (x.HasValue) row.XPosition = x.Value;
                if (y.HasValue) row.YPosition = y.Value;
                if (z.HasValue) row.ZPosition = z.Value;
                if (tiltX.HasValue) row.TiltAboutX = tiltX.Value;
                if (tiltY.HasValue) row.TiltAboutY = tiltY.Value;
                if (tiltZ.HasValue) row.TiltAboutZ = tiltZ.Value;
                if (!string.IsNullOrEmpty(material)) row.Material = material;
                if (comment != null) row.Comment = comment;
                if (refObject.HasValue) row.RefObject = refObject.Value;
                if (insideOf.HasValue) row.InsideOf = insideOf.Value;

                if (parameters != null)
                {
                    int paramCount = NscCellHelper.ParameterCount(row);
                    foreach (var p in parameters)
                    {
                        if (p.Index < 1 || p.Index > paramCount)
                            throw new ArgumentException(
                                $"Parameter index {p.Index} out of range for this object (1-{paramCount}).");

                        var col = (ObjectColumn)((int)ObjectColumn.Par1 + (p.Index - 1));
                        var cell = row.GetObjectCell(col);
                        if (p.Value.HasValue)
                            NscCellHelper.WriteNumeric(cell, p.Value.Value);
                        else if (p.Text != null)
                            cell.Value = p.Text;
                        else
                            throw new ArgumentException(
                                $"Parameter index {p.Index} has neither a numeric 'value' nor a 'text' value.");
                    }
                }

                // Read-back is best-effort and must not mask a successful mutation:
                // the edits above have already been applied at this point.
                List<NscParameterInfo>? readBack = null;
                try { readBack = NscCellHelper.ReadParameters(row); }
                catch { /* leave readBack null; the set itself succeeded */ }

                return new NscSetObjectResult(
                    Success: true,
                    Error: null,
                    UpdatedObject: new NscObjectSummary(
                        objectNumber, row.TypeName ?? row.Type.ToString(), row.Comment ?? "",
                        row.XPosition, row.YPosition, row.ZPosition,
                        row.TiltAboutX, row.TiltAboutY, row.TiltAboutZ,
                        row.Material ?? "", row.RefObject, row.InsideOf),
                    Parameters: readBack
                );
            });
        }
        catch (Exception ex)
        {
            return new NscSetObjectResult(false, ex.Message, null, null);
        }
    }
}

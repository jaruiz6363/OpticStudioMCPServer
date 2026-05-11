using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Models;
using ZemaxMCP.Core.Session;

namespace ZemaxMCP.Server.Tools.LensData;

[McpServerToolType]
public class SetSurfaceTool
{
    private readonly IZemaxSession _session;

    public SetSurfaceTool(IZemaxSession session) => _session = session;

    public record SetSurfaceResult(
        bool Success,
        string? Error,
        Surface UpdatedSurface,
        List<string>? Warnings = null
    );

    [McpServerTool(Name = "zemax_set_surface")]
    [Description("Modify properties of a surface in the lens data editor")]
    public async Task<SetSurfaceResult> ExecuteAsync(
        [Description("Surface number to modify")] int surfaceNumber,
        [Description("Radius of curvature")] double? radius = null,
        [Description("Thickness to next surface")] double? thickness = null,
        [Description("Material/glass name")] string? material = null,
        [Description("Coating name. Must match a coating defined in the currently-loaded coating file (e.g. \"AR\"). Use an empty string to remove the coating.")] string? coating = null,
        [Description("Semi-diameter")] double? semiDiameter = null,
        [Description("Conic constant")] double? conic = null,
        [Description("Surface comment")] string? comment = null,
        [Description("Set as stop surface. Omit to leave unchanged.")] bool? isStop = null,
        [Description("Make radius variable. Omit to leave unchanged.")] bool? radiusVariable = null,
        [Description("Make thickness variable. Omit to leave unchanged.")] bool? thicknessVariable = null,
        [Description("Make conic variable. Omit to leave unchanged.")] bool? conicVariable = null,
        [Description("Minimum bound for thickness variable. Hard constraint the optimizer cannot violate. Requires OpticStudio 2023+.")] double? thicknessMin = null,
        [Description("Maximum bound for thickness variable. Hard constraint the optimizer cannot violate. Requires OpticStudio 2023+.")] double? thicknessMax = null)
    {
        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["surfaceNumber"] = surfaceNumber,
                ["radius"] = radius,
                ["thickness"] = thickness,
                ["material"] = material,
                ["coating"] = coating,
                ["semiDiameter"] = semiDiameter,
                ["conic"] = conic,
                ["comment"] = comment,
                ["isStop"] = isStop,
                ["radiusVariable"] = radiusVariable,
                ["thicknessVariable"] = thicknessVariable,
                ["conicVariable"] = conicVariable,
                ["thicknessMin"] = thicknessMin,
                ["thicknessMax"] = thicknessMax
            };

            var result = await _session.ExecuteAsync("SetSurface", parameters, system =>
            {
                var lde = system.LDE;

                if (surfaceNumber < 0 || surfaceNumber >= lde.NumberOfSurfaces)
                {
                    throw new ArgumentException(
                        $"Invalid surface number: {surfaceNumber}. " +
                        $"Valid range: 0-{lde.NumberOfSurfaces - 1}");
                }

                var surface = lde.GetSurfaceAt(surfaceNumber);

                if (radius.HasValue)
                    surface.Radius = radius.Value;

                if (thickness.HasValue)
                    surface.Thickness = thickness.Value;

                if (!string.IsNullOrEmpty(material))
                    surface.Material = material;

                if (coating is not null)
                    surface.Coating = coating;

                if (semiDiameter.HasValue)
                    surface.SemiDiameter = semiDiameter.Value;

                if (conic.HasValue)
                    surface.Conic = conic.Value;

                if (!string.IsNullOrEmpty(comment))
                    surface.Comment = comment;

                if (isStop.HasValue && isStop.Value)
                    surface.IsStop = true;

                // Set solve status for variables
                // If a cell has an active solve (e.g. Pickup, Position), it must be
                // cleared to Fixed first before it can be made Variable.
                if (radiusVariable.HasValue && radiusVariable.Value)
                {
                    ClearSolveIfNeeded(surface.RadiusCell);
                    surface.RadiusCell.MakeSolveVariable();
                }

                if (thicknessVariable.HasValue && thicknessVariable.Value)
                {
                    ClearSolveIfNeeded(surface.ThicknessCell);
                    surface.ThicknessCell.MakeSolveVariable();
                }

                if (conicVariable.HasValue && conicVariable.Value)
                {
                    ClearSolveIfNeeded(surface.ConicCell);
                    surface.ConicCell.MakeSolveVariable();
                }

                // Set variable bounds (requires OpticStudio 2023+ API)
                var boundsWarnings = new List<string>();
                if (thicknessMin.HasValue || thicknessMax.HasValue)
                {
                    try
                    {
                        dynamic cell = surface.ThicknessCell;
                        if (thicknessMin.HasValue)
                            cell.Min = thicknessMin.Value;
                        if (thicknessMax.HasValue)
                            cell.Max = thicknessMax.Value;
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                    {
                        boundsWarnings.Add(
                            "ThicknessCell.Min/Max properties not available in this OpticStudio version. " +
                            "Variable bounds require OpticStudio 2023 or later. " +
                            "Consider using MNCT/MXCT merit function operands as an alternative.");
                    }
                }

                return new SetSurfaceResult(
                    Success: true,
                    Error: null,
                    UpdatedSurface: new Surface
                    {
                        Number = surfaceNumber,
                        Comment = surface.Comment ?? "",
                        Radius = surface.Radius,
                        Thickness = surface.Thickness,
                        Material = surface.Material,
                        Coating = surface.Coating,
                        SemiDiameter = surface.SemiDiameter,
                        Conic = surface.Conic,
                        SurfaceType = surface.Type.ToString(),
                        IsStop = surface.IsStop
                    },
                    Warnings: boundsWarnings.Count > 0 ? boundsWarnings : null
                );
            });

            return result;
        }
        catch (Exception ex)
        {
            return new SetSurfaceResult(
                Success: false,
                Error: ex.Message,
                UpdatedSurface: new Surface { Number = surfaceNumber }
            );
        }
    }

    private static void ClearSolveIfNeeded(dynamic cell)
    {
        var solveType = cell.Solve;
        if (solveType != ZOSAPI.Editors.SolveType.Fixed &&
            solveType != ZOSAPI.Editors.SolveType.Variable)
        {
            cell.MakeSolveFixed();
        }
    }
}

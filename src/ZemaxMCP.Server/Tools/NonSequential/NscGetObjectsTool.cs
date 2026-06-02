using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;
using ZOSAPI;

namespace ZemaxMCP.Server.Tools.NonSequential;

[McpServerToolType]
public class NscGetObjectsTool
{
    private readonly IZemaxSession _session;

    public NscGetObjectsTool(IZemaxSession session) => _session = session;

    public record NscGetObjectsResult(
        bool Success,
        string? Error,
        string Mode,
        int NumberOfObjects,
        List<NscObjectSummary> Objects
    );

    [McpServerTool(Name = "zemax_nsc_get_objects")]
    [Description("List all objects in the Non-sequential Component Editor (NCE) with their type, comment, " +
                 "position, tilt, material, and reference/inside-of links. Requires the system to be in " +
                 "non-sequential mode (use zemax_set_system_mode).")]
    public async Task<NscGetObjectsResult> ExecuteAsync()
    {
        try
        {
            return await _session.ExecuteAsync("NscGetObjects", null, system =>
            {
                var mode = system.Mode;
                var nce = system.NCE;
                var objects = new List<NscObjectSummary>();

                for (int i = 1; i <= nce.NumberOfObjects; i++)
                {
                    var row = nce.GetObjectAt(i);
                    objects.Add(new NscObjectSummary(
                        ObjectNumber: i,
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
                        InsideOf: row.InsideOf
                    ));
                }

                return new NscGetObjectsResult(
                    Success: true,
                    Error: mode == SystemType.NonSequential
                        ? null
                        : "System is in sequential mode; the NCE may be empty. Switch with zemax_set_system_mode.",
                    Mode: mode.ToString(),
                    NumberOfObjects: nce.NumberOfObjects,
                    Objects: objects
                );
            });
        }
        catch (Exception ex)
        {
            return new NscGetObjectsResult(false, ex.Message, "Unknown", 0, new List<NscObjectSummary>());
        }
    }
}

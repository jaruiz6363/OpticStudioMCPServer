using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;
using ZOSAPI;

namespace ZemaxMCP.Server.Tools.NonSequential;

[McpServerToolType]
public class SetSystemModeTool
{
    private readonly IZemaxSession _session;

    public SetSystemModeTool(IZemaxSession session) => _session = session;

    public record SetSystemModeResult(
        bool Success,
        string? Error,
        string Mode,
        int NumberOfObjects,
        int NumberOfSurfaces
    );

    [McpServerTool(Name = "zemax_set_system_mode")]
    [Description("Switch the optical system between sequential and non-sequential modes, or report the current mode. " +
                 "Non-sequential mode uses the Non-sequential Component Editor (NCE) for illumination, photometry, " +
                 "stray light, and non-imaging simulations. Call with no argument to just report the current mode.")]
    public async Task<SetSystemModeResult> ExecuteAsync(
        [Description("Target mode: 'sequential' or 'nonsequential'. Omit to leave the mode unchanged and just report it.")]
        string? mode = null)
    {
        try
        {
            var parameters = new Dictionary<string, object?> { ["mode"] = mode };

            return await _session.ExecuteAsync("SetSystemMode", parameters, system =>
            {
                if (!string.IsNullOrWhiteSpace(mode))
                {
                    switch (mode!.Trim().ToLowerInvariant())
                    {
                        case "nonsequential":
                        case "non-sequential":
                        case "nsc":
                            if (!system.MakeNonSequential())
                                throw new InvalidOperationException("Failed to switch to non-sequential mode.");
                            break;
                        case "sequential":
                        case "seq":
                            if (!system.MakeSequential())
                                throw new InvalidOperationException("Failed to switch to sequential mode.");
                            break;
                        default:
                            throw new ArgumentException(
                                $"Unknown mode '{mode}'. Use 'sequential' or 'nonsequential'.");
                    }
                }

                var current = system.Mode;
                bool isNsc = current == SystemType.NonSequential;

                return new SetSystemModeResult(
                    Success: true,
                    Error: null,
                    Mode: current.ToString(),
                    NumberOfObjects: isNsc ? system.NCE.NumberOfObjects : 0,
                    NumberOfSurfaces: isNsc ? 0 : system.LDE.NumberOfSurfaces
                );
            });
        }
        catch (Exception ex)
        {
            return new SetSystemModeResult(false, ex.Message, "Unknown", 0, 0);
        }
    }
}

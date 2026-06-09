using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;

namespace ZemaxMCP.Server.Tools.NonSequential;

[McpServerToolType]
public class NscRemoveObjectTool
{
    private readonly IZemaxSession _session;

    public NscRemoveObjectTool(IZemaxSession session) => _session = session;

    public record NscRemoveObjectResult(
        bool Success,
        string? Error,
        int RemovedObject,
        int TotalObjects
    );

    [McpServerTool(Name = "zemax_nsc_remove_object")]
    [Description("Remove an object from the Non-sequential Component Editor (NCE) by its 1-based object number. " +
                 "Note that objects referenced by other objects (RefObject / InsideOf) may shift numbering.")]
    public async Task<NscRemoveObjectResult> ExecuteAsync(
        [Description("Object number to remove (1-based).")] int objectNumber)
    {
        try
        {
            var parameters = new Dictionary<string, object?> { ["objectNumber"] = objectNumber };

            return await _session.ExecuteAsync("NscRemoveObject", parameters, system =>
            {
                var nce = system.NCE;

                if (objectNumber < 1 || objectNumber > nce.NumberOfObjects)
                    throw new ArgumentException(
                        $"Invalid object number: {objectNumber}. Valid range: 1-{nce.NumberOfObjects}.");

                if (!nce.RemoveObjectAt(objectNumber))
                    throw new InvalidOperationException(
                        $"Failed to remove object {objectNumber}. It may be referenced by another object.");

                return new NscRemoveObjectResult(
                    Success: true,
                    Error: null,
                    RemovedObject: objectNumber,
                    TotalObjects: nce.NumberOfObjects
                );
            });
        }
        catch (Exception ex)
        {
            return new NscRemoveObjectResult(false, ex.Message, objectNumber, 0);
        }
    }
}

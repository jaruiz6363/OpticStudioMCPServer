using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;

namespace ZemaxMCP.Server.Tools.NonSequential;

[McpServerToolType]
public class NscListObjectTypesTool
{
    private readonly IZemaxSession _session;

    public NscListObjectTypesTool(IZemaxSession session) => _session = session;

    public record NscObjectTypesResult(
        bool Success,
        string? Error,
        string Filter,
        int Count,
        List<string> TypeNames
    );

    [McpServerTool(Name = "zemax_nsc_list_object_types")]
    [Description("List the non-sequential object type names available in this OpticStudio installation. " +
                 "Use the returned names verbatim when calling zemax_nsc_add_object. " +
                 "Filter by category to narrow the list.")]
    public async Task<NscObjectTypesResult> ExecuteAsync(
        [Description("Category filter: 'all', 'sources', 'detectors', or 'objects' (non-source/non-detector geometry).")]
        string filter = "all")
    {
        try
        {
            var parameters = new Dictionary<string, object?> { ["filter"] = filter };

            return await _session.ExecuteAsync("NscListObjectTypes", parameters, system =>
            {
                var nce = system.NCE;
                var key = (filter ?? "all").Trim().ToLowerInvariant();

                string[] names = key switch
                {
                    "sources" => nce.AvailableSourceNames(),
                    "detectors" => nce.AvailableDetectorNames(),
                    "objects" => nce.AvailableObjectNames(),
                    "all" => nce.AllAvailableObjectNames(),
                    _ => throw new ArgumentException(
                        $"Unknown filter '{filter}'. Use 'all', 'sources', 'detectors', or 'objects'.")
                };

                var list = (names ?? Array.Empty<string>())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new NscObjectTypesResult(true, null, key, list.Count, list);
            });
        }
        catch (Exception ex)
        {
            return new NscObjectTypesResult(false, ex.Message, filter, 0, new List<string>());
        }
    }
}

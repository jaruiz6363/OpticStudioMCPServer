using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;

namespace ZemaxMCP.Server.Tools.NonSequential;

[McpServerToolType]
public class NscRayTraceTool
{
    private readonly IZemaxSession _session;

    public NscRayTraceTool(IZemaxSession session) => _session = session;

    public record NscRayTraceResult(
        bool Success,
        string? Error,
        double TotalRayEnergy,
        string Status,
        bool SplitRays,
        bool ScatterRays,
        bool UsePolarization
    );

    [McpServerTool(Name = "zemax_nsc_ray_trace")]
    [Description("Run a non-sequential ray trace, propagating rays from all sources through the NCE and " +
                 "accumulating energy on detectors. After it completes, read results with " +
                 "zemax_nsc_get_detector_data. Splitting and scattering make the trace physically complete " +
                 "but slower. This call blocks until the trace finishes.")]
    public async Task<NscRayTraceResult> ExecuteAsync(
        [Description("Split rays at interfaces using Fresnel/coating coefficients (reflection + refraction). Slower.")]
        bool splitRays = false,
        [Description("Scatter rays according to each object's scatter model. Slower.")]
        bool scatterRays = false,
        [Description("Use polarization in the trace.")]
        bool usePolarization = false,
        [Description("Continue past ray errors instead of aborting.")]
        bool ignoreErrors = true,
        [Description("Clear all detectors before tracing (recommended for a fresh result).")]
        bool clearDetectors = true,
        [Description("Number of CPU cores to use. 0 lets OpticStudio choose (typically all available).")]
        int cores = 0)
    {
        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["splitRays"] = splitRays,
                ["scatterRays"] = scatterRays,
                ["usePolarization"] = usePolarization,
                ["ignoreErrors"] = ignoreErrors,
                ["clearDetectors"] = clearDetectors,
                ["cores"] = cores
            };

            return await _session.ExecuteAsync("NscRayTrace", parameters, system =>
            {
                var nsc = system.Tools.OpenNSCRayTrace();
                try
                {
                    nsc.SplitNSCRays = splitRays;
                    nsc.ScatterNSCRays = scatterRays;
                    nsc.UsePolarization = usePolarization;
                    nsc.IgnoreErrors = ignoreErrors;
                    if (cores > 0)
                        nsc.NumberOfCores = cores;

                    if (clearDetectors)
                        nsc.ClearDetectors(0); // 0 = clear all detectors

                    bool ok = nsc.RunAndWaitForCompletion();

                    return new NscRayTraceResult(
                        Success: ok,
                        Error: ok ? null : (string.IsNullOrEmpty(nsc.ErrorMessage)
                            ? "NSC ray trace did not complete successfully."
                            : nsc.ErrorMessage),
                        TotalRayEnergy: nsc.GetTotalRayEnergy(),
                        Status: nsc.Status ?? "",
                        SplitRays: splitRays,
                        ScatterRays: scatterRays,
                        UsePolarization: usePolarization
                    );
                }
                finally
                {
                    nsc.Close();
                }
            });
        }
        catch (Exception ex)
        {
            return new NscRayTraceResult(false, ex.Message, 0, "", splitRays, scatterRays, usePolarization);
        }
    }
}

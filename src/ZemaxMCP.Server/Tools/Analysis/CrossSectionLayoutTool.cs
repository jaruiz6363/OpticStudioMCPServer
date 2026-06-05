using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;
using ZOSAPI.Tools.Layouts;

namespace ZemaxMCP.Server.Tools.Analysis;

[McpServerToolType]
public class CrossSectionLayoutTool
{
    private readonly IZemaxSession _session;

    public CrossSectionLayoutTool(IZemaxSession session) => _session = session;

    public record CrossSectionLayoutResult(
        bool Success,
        string? Error,
        string? FilePath,
        int PixelWidth,
        int PixelHeight,
        int StartSurface,
        int EndSurface,
        int NumberOfRays,
        int Field,
        int Wavelength,
        string? Status,
        string? Message);

    [McpServerTool(Name = "zemax_cross_section_layout")]
    [Description("Export the 2D cross-section layout of the lens as a PNG image using the OpticStudio Cross Section export tool. The output file is written to disk by OpticStudio.")]
    public async Task<CrossSectionLayoutResult> ExecuteAsync(
        [Description("Absolute path for the output PNG file. If omitted, the file is written next to the currently open .zos/.zmx file as cross_section_<timestamp>.png. Falls back to %TEMP%\\ZemaxMCP when no file is open.")] string? outputPath = null,
        [Description("Output image width in pixels")] int pixelWidth = 1200,
        [Description("Output image height in pixels")] int pixelHeight = 800,
        [Description("Number of rays to draw per field (use 0 for marginal and chief ray only)")] int numberOfRays = 5,
        [Description("First surface to include in the plot. Use -1 to leave at the OpticStudio default.")] int startSurface = -1,
        [Description("Last surface to include in the plot. Use -1 to leave at the OpticStudio default.")] int endSurface = -1,
        [Description("Field number to plot (1-indexed). Use 0 to plot all fields.")] int field = 0,
        [Description("Wavelength number to plot (1-indexed). Use 0 to plot all wavelengths.")] int wavelength = 0,
        [Description("Configuration number to plot. Use 0 for the current configuration.")] int configuration = 0,
        [Description("Y-axis stretch factor (1.0 = no stretch)")] double yStretch = 1.0,
        [Description("Delete vignetted rays from the plot")] bool deleteVignetted = false,
        [Description("Color rays by 'Fields', 'Waves', or 'Wavelength'")] string colorRaysBy = "Fields")
    {
        try
        {
            if (!Enum.TryParse<ColorRaysByCrossSectionOptions>(colorRaysBy, ignoreCase: true, out var colorRaysByOption))
            {
                colorRaysByOption = ColorRaysByCrossSectionOptions.Fields;
            }

            var parameters = new Dictionary<string, object?>
            {
                ["outputPath"] = outputPath,
                ["pixelWidth"] = pixelWidth,
                ["pixelHeight"] = pixelHeight,
                ["numberOfRays"] = numberOfRays,
                ["startSurface"] = startSurface,
                ["endSurface"] = endSurface,
                ["field"] = field,
                ["wavelength"] = wavelength,
                ["configuration"] = configuration,
                ["yStretch"] = yStretch,
                ["deleteVignetted"] = deleteVignetted,
                ["colorRaysBy"] = colorRaysByOption.ToString()
            };

            return await _session.ExecuteAsync("CrossSectionLayout", parameters, system =>
            {
                var resolvedOutputPath = outputPath;
                if (string.IsNullOrWhiteSpace(resolvedOutputPath))
                {
                    var systemFile = system.SystemFile;
                    var targetDir = !string.IsNullOrWhiteSpace(systemFile)
                        ? Path.GetDirectoryName(systemFile)
                        : null;

                    if (string.IsNullOrWhiteSpace(targetDir))
                    {
                        targetDir = Path.Combine(Path.GetTempPath(), "ZemaxMCP");
                    }

                    Directory.CreateDirectory(targetDir);
                    resolvedOutputPath = Path.Combine(targetDir, $"cross_section_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                }
                else
                {
                    var dir = Path.GetDirectoryName(resolvedOutputPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }

                outputPath = resolvedOutputPath;

                if (File.Exists(outputPath))
                {
                    try { File.Delete(outputPath); } catch { }
                }

                var export = system.Tools.Layouts.OpenCrossSectionExport();
                if (export == null)
                {
                    return new CrossSectionLayoutResult(
                        Success: false,
                        Error: "OpenCrossSectionExport returned null.",
                        FilePath: outputPath,
                        PixelWidth: pixelWidth,
                        PixelHeight: pixelHeight,
                        StartSurface: startSurface,
                        EndSurface: endSurface,
                        NumberOfRays: numberOfRays,
                        Field: field,
                        Wavelength: wavelength,
                        Status: null,
                        Message: null);
                }

                try
                {
                    export.OutputFileName = outputPath;
                    export.SaveImageAsFile = true;
                    export.OutputPixelWidth = pixelWidth;
                    export.OutputPixelHeight = pixelHeight;
                    export.NumberOfRays = Math.Max(0, numberOfRays);
                    export.MarginalAndChiefRayOnly = numberOfRays <= 0;
                    export.YStretch = yStretch;
                    export.DeleteVignetted = deleteVignetted;
                    export.ColorRaysBy = colorRaysByOption;

                    if (startSurface >= 0) export.StartSurface = startSurface;
                    if (endSurface >= 0) export.EndSurface = endSurface;
                    if (configuration > 0) export.Configuration = configuration;

                    if (field <= 0) export.SetFieldsAll();
                    else export.Field = field;

                    if (wavelength <= 0) export.SetWavelengthsAll();
                    else export.Wavelength = wavelength;

                    var ran = export.RunAndWaitForCompletion();
                    var succeeded = export.Succeeded;
                    var status = export.Status?.ToString();
                    var errorMessage = export.ErrorMessage;

                    var fileWritten = File.Exists(outputPath);
                    var success = ran && succeeded && fileWritten;

                    string? error = null;
                    if (!success)
                    {
                        if (!string.IsNullOrEmpty(errorMessage)) error = errorMessage;
                        else if (!ran) error = "RunAndWaitForCompletion returned false.";
                        else if (!succeeded) error = $"Export did not succeed (status: {status}).";
                        else error = "Output PNG file was not produced.";
                    }

                    return new CrossSectionLayoutResult(
                        Success: success,
                        Error: error,
                        FilePath: fileWritten ? outputPath : null,
                        PixelWidth: export.OutputPixelWidth,
                        PixelHeight: export.OutputPixelHeight,
                        StartSurface: export.StartSurface,
                        EndSurface: export.EndSurface,
                        NumberOfRays: export.NumberOfRays,
                        Field: export.Field,
                        Wavelength: export.Wavelength,
                        Status: status,
                        Message: success ? $"Cross-section layout saved to {outputPath}" : null);
                }
                finally
                {
                    try { export.Close(); } catch { }
                }
            });
        }
        catch (Exception ex)
        {
            return new CrossSectionLayoutResult(
                Success: false,
                Error: ex.Message,
                FilePath: outputPath,
                PixelWidth: pixelWidth,
                PixelHeight: pixelHeight,
                StartSurface: startSurface,
                EndSurface: endSurface,
                NumberOfRays: numberOfRays,
                Field: field,
                Wavelength: wavelength,
                Status: null,
                Message: null);
        }
    }
}

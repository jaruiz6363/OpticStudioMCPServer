using System.ComponentModel;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Session;

namespace ZemaxMCP.Server.Tools.NonSequential;

[McpServerToolType]
public class NscGetDetectorDataTool
{
    private readonly IZemaxSession _session;

    public NscGetDetectorDataTool(IZemaxSession session) => _session = session;

    public record NscDetectorDataResult(
        bool Success,
        string? Error,
        int ObjectNumber,
        string DetectorType,
        string Comment,
        int Rows,
        int Cols,
        int DataType,
        double TotalFlux,
        double PeakValue,
        int PeakRow,
        int PeakCol,
        double MinValue,
        double MeanValue,
        long HitPixels,
        List<List<double>>? Grid,
        string? Note
    );

    [McpServerTool(Name = "zemax_nsc_get_detector_data")]
    [Description("Read accumulated results from a non-sequential detector object after a trace " +
                 "(run zemax_nsc_ray_trace first). Returns total flux plus per-pixel statistics " +
                 "(peak, min, mean, and the peak pixel location). Optionally returns the full per-pixel grid.")]
    public async Task<NscDetectorDataResult> ExecuteAsync(
        [Description("Object number of the detector in the NCE (1-based).")] int objectNumber,
        [Description("Per-pixel data type code: 0 = flux per pixel (sums to total flux), " +
                     "1 = irradiance (flux/area), 2 = radiant/luminous intensity (flux/solid angle). Default 0.")]
        int dataType = 0,
        [Description("Include the full per-pixel grid in the result. Can be large; capped by maxGridPixels.")]
        bool includeGrid = false,
        [Description("Maximum total pixels to return when includeGrid is true. If the detector is larger, " +
                     "the grid is omitted and a note is returned instead.")]
        int maxGridPixels = 4096)
    {
        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["objectNumber"] = objectNumber,
                ["dataType"] = dataType,
                ["includeGrid"] = includeGrid,
                ["maxGridPixels"] = maxGridPixels
            };

            return await _session.ExecuteAsync("NscGetDetectorData", parameters, system =>
            {
                var nce = system.NCE;

                if (objectNumber < 1 || objectNumber > nce.NumberOfObjects)
                    throw new ArgumentException(
                        $"Invalid object number: {objectNumber}. Valid range: 1-{nce.NumberOfObjects}.");

                var row = nce.GetObjectAt(objectNumber);

                if (!nce.GetDetectorDimensions(objectNumber, out uint uRows, out uint uCols))
                    throw new InvalidOperationException(
                        $"Object {objectNumber} ('{row.TypeName}') is not a detector or has no data. " +
                        "Run zemax_nsc_ray_trace first, and confirm the object is a detector.");

                int rows = (int)uRows;
                int cols = (int)uCols;

                // Total flux is pixel 0 with data code 0, independent of the per-pixel data type.
                nce.GetDetectorData(objectNumber, 0, 0, out double totalFlux);

                double[,]? data = nce.GetAllDetectorDataSafe(objectNumber, dataType);

                double peak = 0, min = 0, sum = 0;
                int peakRow = 0, peakCol = 0;
                long hits = 0;
                string? note = null;

                if (data != null && data.Length > 0)
                {
                    int gr = data.GetLength(0);
                    int gc = data.GetLength(1);
                    bool first = true;
                    for (int r = 0; r < gr; r++)
                    {
                        for (int c = 0; c < gc; c++)
                        {
                            double v = data[r, c];
                            sum += v;
                            if (v != 0) hits++;
                            if (first || v > peak) { peak = v; peakRow = r; peakCol = c; }
                            if (first || v < min) { min = v; }
                            first = false;
                        }
                    }
                }
                else
                {
                    note = "Detector grid is empty. Ensure a ray trace has been run and rays reached this detector.";
                }

                long totalPixels = (long)rows * cols;
                double mean = totalPixels > 0 ? sum / totalPixels : 0;

                List<List<double>>? grid = null;
                if (includeGrid && data != null && data.Length > 0)
                {
                    if (totalPixels <= maxGridPixels)
                    {
                        grid = new List<List<double>>(data.GetLength(0));
                        for (int r = 0; r < data.GetLength(0); r++)
                        {
                            var rowList = new List<double>(data.GetLength(1));
                            for (int c = 0; c < data.GetLength(1); c++)
                                rowList.Add(data[r, c]);
                            grid.Add(rowList);
                        }
                    }
                    else
                    {
                        note = $"Grid omitted: {totalPixels} pixels exceeds maxGridPixels ({maxGridPixels}). " +
                               "Increase maxGridPixels or reduce detector resolution to retrieve the full grid.";
                    }
                }

                return new NscDetectorDataResult(
                    Success: true,
                    Error: null,
                    ObjectNumber: objectNumber,
                    DetectorType: row.TypeName ?? row.Type.ToString(),
                    Comment: row.Comment ?? "",
                    Rows: rows,
                    Cols: cols,
                    DataType: dataType,
                    TotalFlux: totalFlux,
                    PeakValue: peak,
                    PeakRow: peakRow,
                    PeakCol: peakCol,
                    MinValue: min,
                    MeanValue: mean,
                    HitPixels: hits,
                    Grid: grid,
                    Note: note
                );
            });
        }
        catch (Exception ex)
        {
            return new NscDetectorDataResult(false, ex.Message, objectNumber, "", "",
                0, 0, dataType, 0, 0, 0, 0, 0, 0, 0, null, null);
        }
    }
}

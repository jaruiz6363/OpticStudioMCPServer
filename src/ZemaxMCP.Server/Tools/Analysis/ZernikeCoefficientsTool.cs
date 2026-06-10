using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using ZemaxMCP.Core.Models;
using ZemaxMCP.Core.Session;
using ZOSAPI.Analysis;
using ZOSAPI.Analysis.Settings.Aberrations;

namespace ZemaxMCP.Server.Tools.Analysis;

[McpServerToolType]
public class ZernikeCoefficientsTool(IZemaxSession session)
{
    private readonly IZemaxSession _session = session;

    [McpServerTool(Name = "zemax_zernike_coefficients")]
    [Description("Compute Zernike wavefront coefficients at a single field/wavelength. Supports three polynomial sets via 'type': 'standard' (Zernike Standard, OSA/ANSI ordering), 'annular' (Zernike Annular, orthonormal over an annulus with central obscuration), or 'fringe' (Zernike Fringe, University of Arizona ordering). Returns the per-term coefficients in waves plus summary metrics (RMS, peak-to-valley, Strehl, fit error).")]
    public async Task<ZernikeCoefficientsData> ExecuteAsync(
        [Description("Polynomial set: 'standard', 'annular', or 'fringe'")] string type,
        [Description("Field number (1-based, default 1)")] int field = 1,
        [Description("Wavelength number (0 for primary)")] int wavelength = 0,
        [Description("Maximum number of terms (0 = use OpticStudio default)")] int maxTerms = 0,
        [Description("Sampling (1=32, 2=64, 3=128, 4=256, 5=512, 6=1024)")] int sampling = 3,
        [Description("Annular obscuration ratio 0..1 (used by 'annular'; ignored for others)")] double obscuration = 0.0,
        [Description("Surface number (0 = image surface)")] int surface = 0)
    {
        var typeNormalized = (type ?? "").Trim().ToLowerInvariant();
        if (typeNormalized != "standard" && typeNormalized != "annular" && typeNormalized != "fringe")
        {
            return new ZernikeCoefficientsData
            {
                Success = false,
                Error = $"Invalid type '{type}'. Must be 'standard', 'annular', or 'fringe'."
            };
        }

        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["type"] = typeNormalized,
                ["field"] = field,
                ["wavelength"] = wavelength,
                ["maxTerms"] = maxTerms,
                ["sampling"] = sampling,
                ["obscuration"] = obscuration,
                ["surface"] = surface
            };

            return await _session.ExecuteAsync("ZernikeCoefficients", parameters, system =>
            {
                IA_ analysis = typeNormalized switch
                {
                    "standard" => system.Analyses.New_ZernikeStandardCoefficients(),
                    "annular" => system.Analyses.New_ZernikeAnnularCoefficients(),
                    "fringe" => system.Analyses.New_ZernikeFringeCoefficients(),
                    _ => throw new InvalidOperationException("Unreachable")
                };

                try
                {
                    ApplySettings(analysis, typeNormalized, field, wavelength, maxTerms, sampling, obscuration, surface);

                    analysis.ApplyAndWaitForCompletion();

                    var tmpDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".tmp"));
                    Directory.CreateDirectory(tmpDir);
                    var outputFile = Path.Combine(tmpDir, $"zemax_zernike_{typeNormalized}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.txt");

                    analysis.GetResults().GetTextFile(outputFile);

                    try
                    {
                        return ParseZernikeTextFile(outputFile, typeNormalized);
                    }
                    catch (Exception parseEx)
                    {
                        return new ZernikeCoefficientsData
                        {
                            Success = false,
                            Error = $"Failed to parse Zernike text file: {parseEx.Message}",
                            CoefficientType = typeNormalized,
                            SourceFilePath = outputFile
                        };
                    }
                }
                finally
                {
                    analysis.Close();
                }
            });
        }
        catch (Exception ex)
        {
            return new ZernikeCoefficientsData
            {
                Success = false,
                Error = ex.Message,
                CoefficientType = typeNormalized
            };
        }
    }

    private static void ApplySettings(
        IA_ analysis, string typeNormalized,
        int field, int wavelength, int maxTerms, int sampling,
        double obscuration, int surface)
    {
        var settings = analysis.GetSettings();
        if (settings == null) return;

        var sampleSize = MapSampling(sampling);

        switch (typeNormalized)
        {
            case "standard":
                if (settings is IAS_ZernikeStandardCoefficients std)
                {
                    if (field > 0) std.Field.SetFieldNumber(field);
                    std.Wavelength.SetWavelengthNumber(wavelength);
                    if (surface > 0) std.Surface.SetSurfaceNumber(surface);
                    else std.Surface.UseImageSurface();
                    std.SampleSize = sampleSize;
                    if (maxTerms > 0) std.MaximumNumberOfTerms = maxTerms;
                }
                break;
            case "annular":
                if (settings is IAS_ZernikeAnnularCoefficients ann)
                {
                    if (field > 0) ann.Field.SetFieldNumber(field);
                    ann.Wavelength.SetWavelengthNumber(wavelength);
                    if (surface > 0) ann.Surface.SetSurfaceNumber(surface);
                    else ann.Surface.UseImageSurface();
                    ann.SampleSize = sampleSize;
                    if (maxTerms > 0) ann.MaximumNumberOfTerms = maxTerms;
                    ann.Obscuration = obscuration;
                }
                break;
            case "fringe":
                if (settings is IAS_ZernikeFringeCoefficients fr)
                {
                    if (field > 0) fr.Field.SetFieldNumber(field);
                    fr.Wavelength.SetWavelengthNumber(wavelength);
                    if (surface > 0) fr.Surface.SetSurfaceNumber(surface);
                    else fr.Surface.UseImageSurface();
                    fr.SampleSize = sampleSize;
                    if (maxTerms > 0) fr.MaximumNumberOfTerms = maxTerms;
                }
                break;
        }
    }

    private static SampleSizes MapSampling(int sampling) => sampling switch
    {
        1 => SampleSizes.S_32x32,
        2 => SampleSizes.S_64x64,
        3 => SampleSizes.S_128x128,
        4 => SampleSizes.S_256x256,
        5 => SampleSizes.S_512x512,
        6 => SampleSizes.S_1024x1024,
        _ => SampleSizes.S_128x128
    };

    private static readonly Regex CoefficientRegex = new(
        @"^\s*Z\s*(\d+)\s+([-+]?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)\s*(?::\s*\(.*?\))?\s*(.*)$",
        RegexOptions.Compiled);

    private static ZernikeCoefficientsData ParseZernikeTextFile(string filePath, string typeNormalized)
    {
        var lines = File.ReadAllLines(filePath);

        int fieldNum = 0, waveNum = 0, maxTerm = 0;
        double waveValue = 0;
        string waveUnits = "", surface = "";
        double pvChief = 0, pvCentroid = 0;
        double rmsChief = 0, rmsCentroid = 0;
        double variance = 0, strehl = 0, rmsFit = 0, maxFit = 0;

        var coefficients = new List<ZernikeCoefficient>();

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Coefficient row first — they may collide with prefixes otherwise
            var match = CoefficientRegex.Match(trimmed);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int idx) &&
                    double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    coefficients.Add(new ZernikeCoefficient
                    {
                        Index = idx,
                        Value = val,
                        Description = match.Groups[3].Value.Trim()
                    });
                    continue;
                }
            }

            if (StartsWithIgnoreCase(trimmed, "Surface")
                && trimmed.Contains(':')
                && !StartsWithIgnoreCase(trimmed, "Surface number"))
            {
                surface = ExtractAfterColon(trimmed);
                continue;
            }

            if (StartsWithIgnoreCase(trimmed, "Field") && trimmed.Contains(':') &&
                !StartsWithIgnoreCase(trimmed, "Field type") && !StartsWithIgnoreCase(trimmed, "Fields"))
            {
                var afterColon = ExtractAfterColon(trimmed);
                var firstToken = afterColon.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (firstToken != null && int.TryParse(firstToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int f))
                    fieldNum = f;
                continue;
            }

            if (StartsWithIgnoreCase(trimmed, "Wavelength") && trimmed.Contains(':') &&
                !StartsWithIgnoreCase(trimmed, "Wavelengths"))
            {
                var afterColon = ExtractAfterColon(trimmed);
                var tokens = afterColon.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0 &&
                    double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double wv))
                {
                    waveValue = wv;
                    if (tokens.Length > 1) waveUnits = tokens[1].TrimEnd('.');
                }
                continue;
            }

            if (StartsWithIgnoreCase(trimmed, "Wavelength is") ||
                StartsWithIgnoreCase(trimmed, "Wavelength number"))
            {
                if (TryParseIntFromTail(trimmed, out int wn)) waveNum = wn;
                continue;
            }

            if (StartsWithIgnoreCase(trimmed, "Maximum term"))
            {
                if (TryParseIntFromTail(trimmed, out int mt)) maxTerm = mt;
                continue;
            }

            if (StartsWithIgnoreCase(trimmed, "Peak to Valley (to chief)"))
            {
                pvChief = ParseFirstNumberAfterColon(trimmed);
                continue;
            }
            if (StartsWithIgnoreCase(trimmed, "Peak to Valley (to centroid)"))
            {
                pvCentroid = ParseFirstNumberAfterColon(trimmed);
                continue;
            }
            if (StartsWithIgnoreCase(trimmed, "RMS (to chief)"))
            {
                rmsChief = ParseFirstNumberAfterColon(trimmed);
                continue;
            }
            if (StartsWithIgnoreCase(trimmed, "RMS (to centroid)"))
            {
                rmsCentroid = ParseFirstNumberAfterColon(trimmed);
                continue;
            }
            if (StartsWithIgnoreCase(trimmed, "Variance"))
            {
                variance = ParseFirstNumberAfterColon(trimmed);
                continue;
            }
            if (StartsWithIgnoreCase(trimmed, "Strehl Ratio"))
            {
                strehl = ParseFirstNumberAfterColon(trimmed);
                continue;
            }
            if (StartsWithIgnoreCase(trimmed, "RMS fit error"))
            {
                rmsFit = ParseFirstNumberAfterColon(trimmed);
                continue;
            }
            if (StartsWithIgnoreCase(trimmed, "Maximum fit error"))
            {
                maxFit = ParseFirstNumberAfterColon(trimmed);
                continue;
            }
        }

        return new ZernikeCoefficientsData
        {
            Success = true,
            SourceFilePath = filePath,
            CoefficientType = typeNormalized,
            Field = fieldNum,
            Wavelength = waveNum,
            WavelengthValue = waveValue,
            WavelengthUnits = waveUnits,
            Surface = surface,
            MaximumTerm = maxTerm > 0 ? maxTerm : coefficients.Count,
            PeakToValleyChief = pvChief,
            PeakToValleyCentroid = pvCentroid,
            RmsChief = rmsChief,
            RmsCentroid = rmsCentroid,
            Variance = variance,
            StrehlRatio = strehl,
            RmsFitError = rmsFit,
            MaximumFitError = maxFit,
            Coefficients = coefficients.ToArray()
        };
    }

    private static bool StartsWithIgnoreCase(string s, string prefix) =>
        s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static string ExtractAfterColon(string line)
    {
        int idx = line.IndexOf(':');
        return idx < 0 ? "" : line.Substring(idx + 1).Trim();
    }

    private static double ParseFirstNumberAfterColon(string line)
    {
        var after = ExtractAfterColon(line);
        var tokens = after.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                return val;
        }
        return 0;
    }

    private static bool TryParseIntFromTail(string line, out int value)
    {
        var tokens = line.Split([' ', '\t', ':'], StringSplitOptions.RemoveEmptyEntries);
        for (int i = tokens.Length - 1; i >= 0; i--)
        {
            if (int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;
        }
        value = 0;
        return false;
    }
}

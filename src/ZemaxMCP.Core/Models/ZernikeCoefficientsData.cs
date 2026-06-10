namespace ZemaxMCP.Core.Models;

public record ZernikeCoefficientsData
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string SourceFilePath { get; init; } = "";
    public string CoefficientType { get; init; } = "";
    public int Field { get; init; }
    public int Wavelength { get; init; }
    public double WavelengthValue { get; init; }
    public string WavelengthUnits { get; init; } = "";
    public string Surface { get; init; } = "";
    public int MaximumTerm { get; init; }
    public double PeakToValleyChief { get; init; }
    public double PeakToValleyCentroid { get; init; }
    public double RmsChief { get; init; }
    public double RmsCentroid { get; init; }
    public double Variance { get; init; }
    public double StrehlRatio { get; init; }
    public double RmsFitError { get; init; }
    public double MaximumFitError { get; init; }
    public ZernikeCoefficient[]? Coefficients { get; init; }
}

public record ZernikeCoefficient
{
    public int Index { get; init; }
    public double Value { get; init; }
    public string Description { get; init; } = "";
}

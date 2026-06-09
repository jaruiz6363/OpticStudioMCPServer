namespace ZemaxMCP.Core.Models;

public record Surface
{
    public int Number { get; init; }
    public string Comment { get; init; } = "";
    public double Radius { get; init; }
    public double Thickness { get; init; }
    public string? Material { get; init; }
    public string? Coating { get; init; }
    public double SemiDiameter { get; init; }
    public double Conic { get; init; }
    public string SurfaceType { get; init; } = "Standard";
    public Dictionary<string, double> Parameters { get; init; } = new();
    public bool IsStop { get; init; }
    public string RadiusSolve { get; init; } = "";
    public string ThicknessSolve { get; init; } = "";
    public string ConicSolve { get; init; } = "";
    public string MaterialSolve { get; init; } = "";
    public string? MaterialSubstituteCatalog { get; init; }
}

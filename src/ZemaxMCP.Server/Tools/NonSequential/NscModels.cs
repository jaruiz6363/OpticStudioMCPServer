namespace ZemaxMCP.Server.Tools.NonSequential;

/// <summary>
/// Summary view of a single object (row) in the Non-sequential Component Editor (NCE).
/// </summary>
public record NscObjectSummary(
    int ObjectNumber,
    string Type,
    string Comment,
    double X,
    double Y,
    double Z,
    double TiltX,
    double TiltY,
    double TiltZ,
    string Material,
    int RefObject,
    int InsideOf
);

/// <summary>
/// A single parameter column (Par1..ParN) of an NCE object, paired with its label.
/// </summary>
public record NscParameterInfo(
    int Index,
    string Label,
    string Value,
    double NumericValue
);

/// <summary>
/// Input for setting a parameter column on an NCE object. Provide either a numeric
/// <see cref="Value"/> (most parameters) or a <see cref="Text"/> value (e.g. file names).
/// </summary>
public record NscParameter(
    int Index,
    double? Value = null,
    string? Text = null
);

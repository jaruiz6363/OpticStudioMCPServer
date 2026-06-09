using System;
using System.Collections.Generic;
using System.Globalization;
using ZOSAPI.Editors;
using ZOSAPI.Editors.NCE;

namespace ZemaxMCP.Server.Tools.NonSequential;

/// <summary>
/// Type-aware helpers for reading and writing NCE editor cells, and for enumerating an
/// object's type-specific parameter columns (Par1..ParN) with correctly aligned labels.
///
/// Background: many NCE parameter columns are integer-typed (e.g. a Source Point's
/// "# Layout Rays" / "# Analysis Rays", detector pixel counts, color/wavenumber). Reading
/// or writing such a cell through <c>DoubleValue</c> throws "Expected Double, got 'Integer'".
/// These helpers branch on <see cref="IEditorCell.DataType"/> instead.
///
/// Also: <c>INCERow.AvailableParameters()</c> returns headers for ALL editor columns,
/// indexed by (ObjectColumn - 1). The 10 fixed columns (Comment=1 .. Material=10) come
/// first, then Par1=11, Par2=12, ... So a parameter's label lives at index
/// (<see cref="FixedColumnCount"/> + p), and the number of real parameters is
/// (labels.Length - <see cref="FixedColumnCount"/>).
/// </summary>
internal static class NscCellHelper
{
    /// <summary>Number of fixed NCE columns (Comment..Material) that precede Par1.</summary>
    public const int FixedColumnCount = 10;

    /// <summary>Read a cell's value as a double, using the accessor matching its data type.</summary>
    public static double ReadNumeric(IEditorCell cell) => cell.DataType switch
    {
        CellDataType.Integer => cell.IntegerValue,
        CellDataType.Double => cell.DoubleValue,
        _ => double.TryParse(cell.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0.0
    };

    /// <summary>Write a numeric value to a cell, using the accessor matching its data type.</summary>
    public static void WriteNumeric(IEditorCell cell, double value)
    {
        switch (cell.DataType)
        {
            case CellDataType.Integer:
                cell.IntegerValue = (int)Math.Round(value);
                break;
            case CellDataType.Double:
                cell.DoubleValue = value;
                break;
            default:
                cell.Value = value.ToString(CultureInfo.InvariantCulture);
                break;
        }
    }

    /// <summary>Number of real type-specific parameters (Par1..ParN) for this object.</summary>
    public static int ParameterCount(INCERow row)
    {
        var labels = row.AvailableParameters() ?? Array.Empty<string>();
        return Math.Max(0, labels.Length - FixedColumnCount);
    }

    /// <summary>
    /// Enumerate the object's type-specific parameter columns with correctly aligned labels
    /// and type-safe numeric values. Returned indices are 1-based (Par1 = index 1).
    /// </summary>
    public static List<NscParameterInfo> ReadParameters(INCERow row)
    {
        var labels = row.AvailableParameters() ?? Array.Empty<string>();
        int paramCount = Math.Max(0, labels.Length - FixedColumnCount);
        var list = new List<NscParameterInfo>(paramCount);
        for (int p = 0; p < paramCount; p++)
        {
            var col = (ObjectColumn)((int)ObjectColumn.Par1 + p);
            var cell = row.GetObjectCell(col);
            string label = (FixedColumnCount + p) < labels.Length
                ? (labels[FixedColumnCount + p] ?? $"Par{p + 1}")
                : $"Par{p + 1}";
            list.Add(new NscParameterInfo(p + 1, label, cell.Value ?? "", ReadNumeric(cell)));
        }
        return list;
    }
}

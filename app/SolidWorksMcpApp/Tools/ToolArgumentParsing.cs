using SolidWorksBridge.SolidWorks;

namespace SolidWorksMcpApp.Tools;

internal static class ToolArgumentParsing
{
    public static SwDocType ParseDocType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Document type must not be empty. Use Part, Assembly, or Drawing.", nameof(type));
        }

        return type.Trim().ToLowerInvariant() switch
        {
            "part" or "1" => SwDocType.Part,
            "assembly" or "asm" or "2" => SwDocType.Assembly,
            "drawing" or "drw" or "3" => SwDocType.Drawing,
            _ => throw new ArgumentException($"Unknown document type '{type}'. Use Part, Assembly, or Drawing.", nameof(type))
        };
    }

    public static SwStandardView ParseStandardView(string? view)
    {
        if (string.IsNullOrWhiteSpace(view))
        {
            throw new ArgumentException("Standard view must not be empty. Use front, back, left, right, top, bottom, isometric, trimetric, or dimetric.", nameof(view));
        }

        return view.Trim().ToLowerInvariant() switch
        {
            "front" => SwStandardView.Front,
            "back" => SwStandardView.Back,
            "left" => SwStandardView.Left,
            "right" => SwStandardView.Right,
            "top" => SwStandardView.Top,
            "bottom" => SwStandardView.Bottom,
            "iso" or "isometric" => SwStandardView.Isometric,
            "trimetric" => SwStandardView.Trimetric,
            "dimetric" => SwStandardView.Dimetric,
            _ => throw new ArgumentException(
                $"Unknown standard view '{view}'. Use front, back, left, right, top, bottom, isometric, trimetric, or dimetric.",
                nameof(view))
        };
    }

    public static SelectableEntityType ParseSelectableEntityType(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Entity type must not be empty. Use Face, Edge, or Vertex.", parameterName);
        }

        if (Enum.TryParse<SelectableEntityType>(value.Trim(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException(
            $"Unknown selectable entity type '{value}'. Use Face, Edge, or Vertex.",
            parameterName);
    }

    public static SketchTextJustification ParseSketchTextJustification(string? value, string parameterName = "justification")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SketchTextJustification.Left;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "left" or "0" => SketchTextJustification.Left,
            "center" or "centre" or "1" => SketchTextJustification.Center,
            "right" or "2" => SketchTextJustification.Right,
            "full" or "fullyjustified" or "fully_justified" or "justified" or "3" => SketchTextJustification.FullyJustified,
            _ => throw new ArgumentException(
                $"Unknown sketch text justification '{value}'. Use left, center, right, or fullyJustified.",
                parameterName)
        };
    }

    public static EndCondition ParseEndCondition(int value, string parameterName = "endCondition") =>
        value switch
        {
            0 => EndCondition.Blind,
            1 => EndCondition.ThroughAll,
            6 => EndCondition.MidPlane,
            _ => throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "endCondition must be 0 (Blind), 1 (ThroughAll), or 6 (MidPlane).")
        };

    public static MateAlign ParseMateAlign(int value, string parameterName = "align") =>
        value switch
        {
            0 => MateAlign.None,
            1 => MateAlign.AntiAligned,
            2 => MateAlign.Closest,
            _ => throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "align must be 0 (None), 1 (AntiAligned), or 2 (Closest).")
        };
}

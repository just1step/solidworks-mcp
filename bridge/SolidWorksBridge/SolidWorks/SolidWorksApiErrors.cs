using System.Text.RegularExpressions;
using SolidWorks.Interop.swconst;
using SolidWorksBridge.Models;

namespace SolidWorksBridge.SolidWorks;

public record SwCodeInfo(int Value, string Name, string Description);

public record SwApiDiagnostics(
    int RawErrorCode,
    IReadOnlyList<SwCodeInfo> Errors,
    int RawWarningCode,
    IReadOnlyList<SwCodeInfo> Warnings);

public sealed class SolidWorksApiException : InvalidOperationException
{
    public SolidWorksApiException(
        string apiName,
        string summary,
        int pipeErrorCode = PipeErrorCodes.SolidWorksOperationFailed,
        SwApiDiagnostics? diagnostics = null,
        IDictionary<string, object?>? context = null,
        Exception? innerException = null)
        : base(BuildMessage(apiName, summary, diagnostics, context), innerException)
    {
        ApiName = apiName;
        Summary = summary;
        PipeErrorCode = pipeErrorCode;
        Diagnostics = diagnostics;
        Context = context == null
            ? null
            : new Dictionary<string, object?>(context);
    }

    public string ApiName { get; }
    public string Summary { get; }
    public int PipeErrorCode { get; }
    public SwApiDiagnostics? Diagnostics { get; }
    public IReadOnlyDictionary<string, object?>? Context { get; }

    public object ToErrorData() => new
    {
        apiName = ApiName,
        summary = Summary,
        diagnostics = Diagnostics,
        context = Context,
    };

    private static string BuildMessage(
        string apiName,
        string summary,
        SwApiDiagnostics? diagnostics,
        IDictionary<string, object?>? context)
    {
        var parts = new List<string>
        {
            $"SolidWorks API '{apiName}' failed: {summary}"
        };

        if (diagnostics != null)
        {
            if (diagnostics.Errors.Count > 0)
            {
                var errorText = string.Join("; ", diagnostics.Errors.Select(code => $"{code.Name} ({code.Value}): {code.Description}"));
                parts.Add($"Errors={diagnostics.RawErrorCode} [{errorText}]");
            }

            if (diagnostics.Warnings.Count > 0)
            {
                var warningText = string.Join("; ", diagnostics.Warnings.Select(code => $"{code.Name} ({code.Value}): {code.Description}"));
                parts.Add($"Warnings={diagnostics.RawWarningCode} [{warningText}]");
            }
        }

        if (context != null && context.Count > 0)
        {
            var contextText = string.Join(", ",
                context.Where(item => item.Value != null)
                    .Select(item => $"{item.Key}={item.Value}"));
            if (!string.IsNullOrWhiteSpace(contextText))
            {
                parts.Add($"Context: {contextText}");
            }
        }

        return string.Join(" | ", parts);
    }
}

public static class SolidWorksApiErrorFactory
{
    public static SolidWorksApiException NotConnected() =>
        new(
            apiName: "SolidWorks.Connection",
            summary: "Not connected to SolidWorks. Call Connect() first.",
            pipeErrorCode: PipeErrorCodes.SolidWorksNotConnected);

    public static SolidWorksApiException FromComException(
        string apiName,
        System.Runtime.InteropServices.COMException ex,
        IDictionary<string, object?>? context = null)
    {
        var diagnostics = new SwApiDiagnostics(
            ex.HResult,
            [new SwCodeInfo(ex.HResult, GetHResultName(ex.HResult), GetHResultDescription(ex.HResult))],
            0,
            Array.Empty<SwCodeInfo>());

        return new SolidWorksApiException(
            apiName,
            ex.Message,
            diagnostics: diagnostics,
            context: context,
            innerException: ex);
    }

    public static SolidWorksApiException FromLoadFailure(
        string apiName,
        string summary,
        int errors,
        int warnings = 0,
        IDictionary<string, object?>? context = null) =>
        new(apiName, summary, diagnostics: CreateLoadDiagnostics(errors, warnings), context: context);

    public static SolidWorksApiException FromSaveFailure(
        string apiName,
        string summary,
        int errors,
        int warnings = 0,
        IDictionary<string, object?>? context = null) =>
        new(apiName, summary, diagnostics: CreateSaveDiagnostics(errors, warnings), context: context);

    public static SolidWorksApiException FromMateFailure(
        string apiName,
        string summary,
        int errorCode,
        IDictionary<string, object?>? context = null) =>
        new(apiName, summary, diagnostics: CreateMateDiagnostics(errorCode), context: context);

    public static SolidWorksApiException FromValidationFailure(
        string apiName,
        string summary,
        IDictionary<string, object?>? context = null) =>
        new(apiName, summary, context: context);

    public static SwApiDiagnostics CreateLoadDiagnostics(int errors, int warnings) =>
        new(errors, DecodeFlags<swFileLoadError_e>(errors), warnings, DecodeFlags<swFileLoadWarning_e>(warnings));

    public static SwApiDiagnostics CreateSaveDiagnostics(int errors, int warnings) =>
        new(errors, DecodeFlags<swFileSaveError_e>(errors), warnings, DecodeFlags<swFileSaveWarning_e>(warnings));

    public static SwApiDiagnostics CreateMateDiagnostics(int errorCode)
    {
        var code = errorCode == 0
            ? new SwCodeInfo(0, "UnknownMateStatus", "SolidWorks returned 0, which is not the documented success value for swAddMateError_e.")
            : DecodeValue<swAddMateError_e>(errorCode);
        return new SwApiDiagnostics(errorCode, [code], 0, Array.Empty<SwCodeInfo>());
    }

    public static IReadOnlyList<SwCodeInfo> DecodeFlags<TEnum>(int rawCode)
        where TEnum : struct, Enum
    {
        if (rawCode == 0)
        {
            return Array.Empty<SwCodeInfo>();
        }

        var entries = Enum.GetValues<TEnum>()
            .Select(value => (Value: Convert.ToInt32(value), Name: Enum.GetName(typeof(TEnum), value)!))
            .Where(entry => entry.Value != 0)
            .GroupBy(entry => entry.Value)
            .Select(group => group.First())
            .OrderByDescending(entry => entry.Value)
            .ToList();

        var exact = entries.FirstOrDefault(entry => entry.Value == rawCode);
        if (exact.Name != null)
        {
            return [new SwCodeInfo(exact.Value, exact.Name, DescribeEnumName(exact.Name))];
        }

        var matches = entries
            .Where(entry => IsPowerOfTwo(entry.Value) && (rawCode & entry.Value) == entry.Value)
            .Select(entry => new SwCodeInfo(entry.Value, entry.Name, DescribeEnumName(entry.Name)))
            .ToList();

        return matches.Count > 0
            ? matches
            : [new SwCodeInfo(rawCode, $"Unknown{typeof(TEnum).Name}", $"Unknown SolidWorks {typeof(TEnum).Name} value: {rawCode}.")];
    }

    public static SwCodeInfo DecodeValue<TEnum>(int rawCode)
        where TEnum : struct, Enum
    {
        var name = Enum.GetName(typeof(TEnum), rawCode);
        return name != null
            ? new SwCodeInfo(rawCode, name, DescribeEnumName(name))
            : new SwCodeInfo(rawCode, $"Unknown{typeof(TEnum).Name}", $"Unknown SolidWorks {typeof(TEnum).Name} value: {rawCode}.");
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    private static string DescribeEnumName(string name)
    {
        return name switch
        {
            "swAddMateError_NoError" => "Mate created successfully.",
            "swAddMateError_IncorrectSelections" => "The current SolidWorks selection set is not valid for this mate.",
            "swAddMateError_FailedToCreateMate" => "SolidWorks could not create the requested mate from the current selections.",
            _ => Humanize(name),
        };
    }

    private static string Humanize(string name)
    {
        var simplified = name.Contains('_')
            ? name[(name.LastIndexOf('_') + 1)..]
            : name;
        simplified = Regex.Replace(simplified, "([a-z0-9])([A-Z])", "$1 $2");
        simplified = simplified.Replace('_', ' ');
        return char.ToUpperInvariant(simplified[0]) + simplified[1..] + ".";
    }

    private static string GetHResultName(int hresult) => hresult switch
    {
        unchecked((int)0x80010001) => "RPC_E_CALL_REJECTED",
        unchecked((int)0x8001010A) => "RPC_E_SERVERCALL_RETRYLATER",
        unchecked((int)0x80004005) => "E_FAIL",
        unchecked((int)0x80070005) => "E_ACCESSDENIED",
        _ => $"HRESULT_0x{hresult:X8}",
    };

    private static string GetHResultDescription(int hresult) => hresult switch
    {
        unchecked((int)0x80010001) => "SolidWorks rejected the COM call, usually because it is busy or showing a modal dialog.",
        unchecked((int)0x8001010A) => "SolidWorks asked the caller to retry later, usually because it is busy rebuilding or a dialog is open.",
        unchecked((int)0x80004005) => "SolidWorks reported an unspecified COM failure.",
        unchecked((int)0x80070005) => "Access denied while calling the SolidWorks COM API.",
        _ => "Unhandled SolidWorks COM error.",
    };
}
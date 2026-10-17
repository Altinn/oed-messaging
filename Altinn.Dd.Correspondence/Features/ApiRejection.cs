using System.Text.Json;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Features;

/// <summary>
/// Turns an API rejection that carries a problem document into a failed result, keeping Altinn's
/// numeric error code and the HTTP status alongside the detail text.
/// </summary>
internal static class ApiRejection
{
    public static CorrespondenceResult<T> ToFailure<T>(AltinnCorrespondenceException<ProblemDetails> rejection) =>
        CorrespondenceResult<T>.Failure(rejection.Result.Detail, ErrorCodeOf(rejection.Result), rejection.StatusCode);

    // Altinn puts its numeric code in the "errorCode" extension of the problem document; the detail
    // text carries only the message.
    private static int? ErrorCodeOf(ProblemDetails problem) =>
        problem.AdditionalProperties is not null
        && problem.AdditionalProperties.TryGetValue("errorCode", out var value)
            ? value switch
            {
                JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt32(out var code) => code,
                JsonElement { ValueKind: JsonValueKind.String } element when int.TryParse(element.GetString(), out var code) => code,
                int code => code,
                string text when int.TryParse(text, out var code) => code,
                _ => null
            }
            : null;
}

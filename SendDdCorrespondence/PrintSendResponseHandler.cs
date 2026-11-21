using System.Text.Json;

namespace SendDdCorrespondence;

// Prints the raw JSON body Altinn returns for the send (POST) request.
internal sealed class PrintSendResponseHandler : DelegatingHandler
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (request.Method == HttpMethod.Post)
        {
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            var pretty = JsonSerializer.Serialize(document.RootElement, Indented);
            Console.WriteLine($"Send response ({(int)response.StatusCode}):\n{pretty}");
        }
        return response;
    }
}

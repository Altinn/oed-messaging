namespace SendDdCorrespondence;

// Prints the raw JSON body Altinn returns for the send (POST) request.
internal class PrintSendResponseHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (request.Method == HttpMethod.Post)
        {
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var pretty = System.Text.Json.JsonSerializer.Serialize(
                System.Text.Json.JsonDocument.Parse(body).RootElement,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Send response ({(int)response.StatusCode}):\n{pretty}");
        }
        return response;
    }
}

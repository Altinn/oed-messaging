using Altinn.Dd.Correspondence.Exceptions;
using Altinn.Dd.Correspondence.Features;
using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Services;

public interface IDdCorrespondenceService
{
    /// <summary>
    /// Creates a new correspondence element in Altinn 3.
    /// </summary>
    /// <param name="correspondence">The correspondence details including recipient, content, and notifications.</param>
    /// <returns>A receipt indicating whether the correspondence was successfully created.</returns>
    /// <exception cref="CorrespondenceServiceException">Thrown when the correspondence creation fails.</exception>
    Task<CorrespondenceResult> SendCorrespondence(DdCorrespondenceDetails correspondence);

    Task<Features.Search.Result> Search(Query query);

    Task<Features.Get.Result> Get(Features.Get.Request request);
}

/// <summary>
/// The <see cref="DdCorrespondenceService"/> class is an implementation of the <see cref="IDdCorrespondenceService"/> interface and represents
/// a wrapper around the Altinn 3 Correspondence API client. This service maintains compatibility with the existing Altinn 2 interface
/// while leveraging the modern Altinn 3 REST API for improved performance and reliability.
/// </summary>
public sealed class DdCorrespondenceService : IDdCorrespondenceService
{
    private readonly IHandler<DdCorrespondenceDetails, CorrespondenceResult> _send;
    private readonly IHandler<Query, Features.Search.Result> _search;
    private readonly IHandler<Features.Get.Request, Features.Get.Result> _get;

    public DdCorrespondenceService(
        IHandler<DdCorrespondenceDetails, CorrespondenceResult> send,
        IHandler<Query, Features.Search.Result> search,
        IHandler<Features.Get.Request, Features.Get.Result> get)
    {
        _send = send;
        _search = search;
        _get = get;
    }

    /// <inheritdoc />        
    public Task<CorrespondenceResult> SendCorrespondence(DdCorrespondenceDetails correspondence)
        => _send.Handle(correspondence);

    public Task<Features.Search.Result> Search(Query query)
        => _search.Handle(query);

    public Task<Features.Get.Result> Get(Features.Get.Request request)
        => _get.Handle(request);
}

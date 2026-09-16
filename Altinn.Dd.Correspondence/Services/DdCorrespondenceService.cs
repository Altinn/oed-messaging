using Altinn.Dd.Correspondence.Features;
using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Services;

/// <summary>
/// Sends, searches and retrieves Altinn 3 correspondence. Register an implementation with
/// <see cref="Extensions.ServiceCollectionExtensions"/> and inject this interface.
/// </summary>
public interface IDdCorrespondenceService
{
    /// <summary>
    /// Creates a new correspondence element in Altinn 3.
    /// </summary>
    /// <param name="correspondence">The correspondence details including recipient, content, and notifications.</param>
    /// <returns>A receipt indicating whether the correspondence was successfully created.</returns>
    /// <remarks>An API rejection is returned as a failure result rather than thrown. A duplicate
    /// idempotency key (409) and any unexpected status still throw; see the package README.</remarks>
    Task<CorrespondenceResult> SendCorrespondence(DdCorrespondenceDetails correspondence);

    /// <summary>
    /// Finds the ids of correspondences matching a query.
    /// </summary>
    /// <param name="query">The search filters. ResourceId and Role are required.</param>
    /// <returns>The matching correspondence ids, or a failure result.</returns>
    Task<Features.Search.Result> Search(Query query);

    /// <summary>
    /// Retrieves the overview of a single correspondence.
    /// </summary>
    /// <param name="request">The correspondence to retrieve.</param>
    /// <returns>The correspondence overview, or a failure result.</returns>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DdCorrespondenceService"/> class.
    /// </summary>
    /// <param name="send">Handler for the send operation.</param>
    /// <param name="search">Handler for the search operation.</param>
    /// <param name="get">Handler for the get operation.</param>
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

    /// <inheritdoc />
    public Task<Features.Search.Result> Search(Query query)
        => _search.Handle(query);

    /// <inheritdoc />
    public Task<Features.Get.Result> Get(Features.Get.Request request)
        => _get.Handle(request);
}

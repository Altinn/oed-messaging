using Altinn.Dd.Correspondence.Features;
using Altinn.Dd.Correspondence.Features.Get;
using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.Models;
using PurgeRequest = Altinn.Dd.Correspondence.Features.Purge.Request;

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
    /// idempotency key (409) and any unexpected status still throw; see the package README.
    /// A 409 means Altinn already holds a correspondence under the key. Check that it is the one
    /// intended before sending again under a new key.</remarks>
    Task<CorrespondenceResult<ReceiptExternal>> SendCorrespondence(DdCorrespondenceDetails correspondence);

    /// <summary>
    /// Finds the ids of correspondences matching a query.
    /// </summary>
    /// <param name="query">The search filters. ResourceId and Role are required.</param>
    /// <returns>The matching correspondence ids, or a failure result.</returns>
    Task<CorrespondenceResult<IEnumerable<Guid>>> Search(Query query);

    /// <summary>
    /// Retrieves the overview of a single correspondence.
    /// </summary>
    /// <param name="request">The correspondence to retrieve.</param>
    /// <returns>The correspondence overview, or a failure result.</returns>
    Task<CorrespondenceResult<CorrespondenceOverview>> Get(Request request);

    /// <summary>
    /// Retrieves the id of the Dialogporten dialog a correspondence belongs to, so later
    /// correspondences can be sent to the same dialog through <see cref="DdCorrespondenceDetails.DialogId"/>.
    /// </summary>
    /// <param name="request">The correspondence whose dialog to look up.</param>
    /// <returns>The dialog id, <c>null</c> if Altinn has not created the dialog yet, or a failure
    /// result.</returns>
    /// <remarks>Altinn creates the dialog in the background after accepting the send, so a lookup
    /// straight after sending usually returns <c>null</c>. Retry until it returns an id, with a
    /// deadline: if the dialog cannot be created, Altinn fails the correspondence and the id stays
    /// <c>null</c>. <see cref="Get"/> shows that as <c>CorrespondenceStatus.Failed</c>.</remarks>
    Task<CorrespondenceResult<Guid?>> GetDialogId(Request request);

    /// <summary>
    /// Purges a correspondence as its sender. Altinn allows that only before the correspondence is
    /// published.
    /// </summary>
    /// <param name="request">The correspondence to purge.</param>
    /// <returns>The id of the purged correspondence, or a failure result.</returns>
    /// <remarks>Use it to withdraw a correspondence that will never be delivered as intended, such as
    /// one sent to a dialog the recipient has since deleted: Altinn accepts that send, cannot add the
    /// transmission, and would otherwise publish the correspondence hours later without one. A
    /// published correspondence is refused with error code 1013. After a purge, <see cref="Get"/>
    /// returns error code 1001 (HTTP 404) for the correspondence. The call is retried on a 5xx, so a
    /// purge whose first attempt went through can come back as 1014 ("already purged") or 1001.</remarks>
    Task<CorrespondenceResult<Guid>> Purge(PurgeRequest request);
}

/// <summary>
/// The <see cref="DdCorrespondenceService"/> class is an implementation of the <see cref="IDdCorrespondenceService"/> interface and represents
/// a wrapper around the Altinn 3 Correspondence API client. Each operation forwards to its feature handler.
/// </summary>
public sealed class DdCorrespondenceService : IDdCorrespondenceService
{
    private readonly IHandler<DdCorrespondenceDetails, CorrespondenceResult<ReceiptExternal>> _send;
    private readonly IHandler<Query, CorrespondenceResult<IEnumerable<Guid>>> _search;
    private readonly IHandler<Request, CorrespondenceResult<CorrespondenceOverview>> _get;
    private readonly IHandler<PurgeRequest, CorrespondenceResult<Guid>> _purge;

    /// <summary>
    /// Initializes a new instance of the <see cref="DdCorrespondenceService"/> class.
    /// </summary>
    /// <param name="send">Handler for the send operation.</param>
    /// <param name="search">Handler for the search operation.</param>
    /// <param name="get">Handler for the get operation.</param>
    /// <param name="purge">Handler for the purge operation.</param>
    public DdCorrespondenceService(
        IHandler<DdCorrespondenceDetails, CorrespondenceResult<ReceiptExternal>> send,
        IHandler<Query, CorrespondenceResult<IEnumerable<Guid>>> search,
        IHandler<Request, CorrespondenceResult<CorrespondenceOverview>> get,
        IHandler<PurgeRequest, CorrespondenceResult<Guid>> purge)
    {
        _send = send;
        _search = search;
        _get = get;
        _purge = purge;
    }

    /// <inheritdoc />
    public Task<CorrespondenceResult<ReceiptExternal>> SendCorrespondence(DdCorrespondenceDetails correspondence)
        => _send.Handle(correspondence);

    /// <inheritdoc />
    public Task<CorrespondenceResult<IEnumerable<Guid>>> Search(Query query)
        => _search.Handle(query);

    /// <inheritdoc />
    public Task<CorrespondenceResult<CorrespondenceOverview>> Get(Request request)
        => _get.Handle(request);

    /// <inheritdoc />
    public async Task<CorrespondenceResult<Guid?>> GetDialogId(Request request)
    {
        var overview = await _get.Handle(request);

        // Pass a rejection on with its error code and status, not just its text.
        return overview.IsSuccess
            ? CorrespondenceResult<Guid?>.Success(overview.Value!.DialogId)
            : CorrespondenceResult<Guid?>.Failure(overview.Error, overview.ErrorCode, overview.StatusCode);
    }

    /// <inheritdoc />
    public Task<CorrespondenceResult<Guid>> Purge(PurgeRequest request)
        => _purge.Handle(request);
}

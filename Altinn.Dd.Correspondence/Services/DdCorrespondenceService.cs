using Altinn.Dd.Correspondence.Features;
using Altinn.Dd.Correspondence.Features.Get;
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
    /// <remarks>An API rejection is returned as a failure result rather than thrown. Any unexpected
    /// status still throws; see the package README.
    /// A duplicate idempotency key (409) means the correspondence already exists, so it returns the
    /// receipt of that correspondence as a success. That receipt carries the correspondence's
    /// current status and no notification orders. The 409 is thrown when the lookup finds nothing or
    /// fails; when the resilience pipeline rejects the lookup,
    /// <see cref="Exceptions.CorrespondenceServiceException"/> is thrown instead.</remarks>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DdCorrespondenceService"/> class.
    /// </summary>
    /// <param name="send">Handler for the send operation.</param>
    /// <param name="search">Handler for the search operation.</param>
    /// <param name="get">Handler for the get operation.</param>
    public DdCorrespondenceService(
        IHandler<DdCorrespondenceDetails, CorrespondenceResult<ReceiptExternal>> send,
        IHandler<Query, CorrespondenceResult<IEnumerable<Guid>>> search,
        IHandler<Request, CorrespondenceResult<CorrespondenceOverview>> get)
    {
        _send = send;
        _search = search;
        _get = get;
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
        => (await _get.Handle(request)).Match(
            onSuccess: overview => CorrespondenceResult<Guid?>.Success(overview.DialogId),
            onFailure: CorrespondenceResult<Guid?>.Failure);
}

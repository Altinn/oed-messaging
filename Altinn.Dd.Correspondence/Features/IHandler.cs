namespace Altinn.Dd.Correspondence.Features;

/// <summary>
/// Handles a single correspondence operation. Each feature under <c>Features</c> supplies one
/// implementation, which <see cref="Services.DdCorrespondenceService"/> resolves from the container.
/// </summary>
/// <typeparam name="TRequest">The request the operation takes.</typeparam>
/// <typeparam name="TResult">The result the operation returns.</typeparam>
public interface IHandler<TRequest, TResult>
{
    /// <summary>
    /// Executes the operation.
    /// </summary>
    /// <param name="request">The request to execute.</param>
    /// <returns>The result of the operation.</returns>
    Task<TResult> Handle(TRequest request);
}

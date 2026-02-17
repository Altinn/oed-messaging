namespace Altinn.Dd.Correspondence.Features;

public interface IHandler<TRequest, TResult>
{
    Task<TResult> Handle(TRequest request);
}

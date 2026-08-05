namespace Anamnesis.Application.Contracts;

public interface IObsPreflight
{
    Task PrepararAsync(CancellationToken cancellationToken);
}

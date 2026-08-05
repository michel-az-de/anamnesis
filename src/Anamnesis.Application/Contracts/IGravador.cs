namespace Anamnesis.Application.Contracts;

public interface IGravador
{
    Task IniciarAsync(CancellationToken cancellationToken);
    Task<string> FinalizarAsync(CancellationToken cancellationToken);
}

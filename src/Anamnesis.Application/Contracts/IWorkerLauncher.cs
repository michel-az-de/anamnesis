namespace Anamnesis.Application.Contracts;

public interface IWorkerLauncher
{
    Task IniciarAsync(CancellationToken cancellationToken);
}

namespace Anamnesis.Application.Contracts;

public interface IArtefatoLauncher
{
    Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken);

    Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken);
}

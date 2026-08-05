namespace Anamnesis.Application.Contracts;

public interface IGravacaoLixeira
{
    Task<bool> ExisteAsync(string caminhoArquivo, CancellationToken cancellationToken);
    Task MoverAsync(string caminhoArquivo, CancellationToken cancellationToken);
}

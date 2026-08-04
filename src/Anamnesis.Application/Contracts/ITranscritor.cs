using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface ITranscritor
{
    Task<TranscricaoGerada> TranscreverAsync(string caminhoArquivo, CancellationToken cancellationToken);
}

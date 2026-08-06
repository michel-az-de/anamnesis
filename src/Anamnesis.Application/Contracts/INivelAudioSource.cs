using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface INivelAudioSource
{
    Task<NivelAudioLeitura> LerAsync(CancellationToken cancellationToken);
}

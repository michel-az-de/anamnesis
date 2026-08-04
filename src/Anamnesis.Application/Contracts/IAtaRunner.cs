using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.Contracts;

public interface IAtaRunner
{
    string Nome { get; }
    Task<AtaGerada> GerarAsync(Reuniao reuniao, TranscricaoGerada transcricao, CancellationToken cancellationToken);
}

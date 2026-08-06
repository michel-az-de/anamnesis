using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface ISinaisReuniaoSource
{
    Task<SinaisDeteccaoReuniao> ObterAsync(CancellationToken cancellationToken);
}

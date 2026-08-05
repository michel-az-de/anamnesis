using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface IReuniaoQuery
{
    Task<IReadOnlyList<ReuniaoResumo>> ListarAsync(
        ReuniaoQueryFiltro filtro,
        CancellationToken cancellationToken);

    Task<ReuniaoDetalhe?> ObterDetalheAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken);
}


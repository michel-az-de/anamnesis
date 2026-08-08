using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.Contracts;

public interface IAgendaCache
{
    Task SalvarContaAsync(ContaAgenda conta, CancellationToken ct = default);
    Task<ContaAgenda?> ObterContaAsync(string contaAgendaId, CancellationToken ct = default);
    Task<IReadOnlyList<ContaAgenda>> ListarContasAsync(CancellationToken ct = default);
    Task RemoverContaAsync(string contaAgendaId, CancellationToken ct = default);

    Task SalvarEventosAsync(string contaAgendaId, IEnumerable<EventoAgenda> eventos, CancellationToken ct = default);
    Task<IReadOnlyList<EventoAgenda>> ListarEventosAsync(string contaAgendaId, CancellationToken ct = default);
    Task<IReadOnlyList<EventoAgenda>> ListarEventosProximosAsync(int minutos = 30, CancellationToken ct = default);
    Task RemoverEventosForaDaJanelaAsync(string contaAgendaId, string inicio, string fim, CancellationToken ct = default);
}

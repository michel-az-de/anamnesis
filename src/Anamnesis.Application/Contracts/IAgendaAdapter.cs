using System.Threading;
using System.Threading.Tasks;
using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.Contracts;

public interface IAgendaAdapter
{
    string Provider { get; }
    Task<ResultadoAutenticacao> IniciarAutenticacaoAsync(CancellationToken ct = default);
    Task<ResultadoSincronizacao> SincronizarAsync(ContaAgenda conta, CancellationToken ct = default);
    Task RevogarAsync(ContaAgenda conta, CancellationToken ct = default);
}

public record ResultadoAutenticacao(bool Sucesso, string? Erro = null, string? ContaId = null);
public record ResultadoSincronizacao(bool Sucesso, string? Erro = null, int EventosProcessados = 0);

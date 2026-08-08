using System.Threading;
using System.Threading.Tasks;

namespace Anamnesis.Application.Contracts;

public interface IAgendaTokenStore
{
    Task SalvarAsync(string chave, string jsonToken, CancellationToken ct = default);
    Task<string?> RecuperarAsync(string chave, CancellationToken ct = default);
    Task RemoverAsync(string chave, CancellationToken ct = default);
}

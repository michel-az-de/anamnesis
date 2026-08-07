using Anamnesis.Application.Contracts;
using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.UseCases;

public sealed class CriarLembreteTarefaHandler(
    ILembreteTarefaRepository repository,
    TimeProvider relogio)
{
    public async Task<LembreteTarefa> ExecutarAsync(
        Guid reuniaoId,
        string descricaoTarefa,
        DateTimeOffset lembrarEm,
        CancellationToken cancellationToken)
    {
        var agora = relogio.GetUtcNow();
        if (lembrarEm <= agora)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lembrarEm),
                "O horario do lembrete deve estar no futuro.");
        }

        var lembrete = LembreteTarefa.Criar(
            Guid.NewGuid(),
            reuniaoId,
            descricaoTarefa,
            lembrarEm,
            agora);
        await repository.SalvarAsync(lembrete, cancellationToken);
        return lembrete;
    }
}

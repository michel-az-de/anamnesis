using Anamnesis.Application.Contracts;

namespace Anamnesis.Application.UseCases;

public sealed class DispararLembretesTarefaHandler(
    ILembreteTarefaRepository repository,
    INotificadorLembrete notificador,
    TimeProvider relogio)
{
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        var agora = relogio.GetUtcNow();
        var pendentes = await repository.ListarPendentesAteAsync(agora, cancellationToken);
        var notificados = 0;
        foreach (var lembrete in pendentes)
        {
            await notificador.NotificarAsync(lembrete, cancellationToken);
            lembrete.MarcarNotificado(agora);
            await repository.SalvarAsync(lembrete, cancellationToken);
            notificados++;
        }

        return notificados;
    }
}

using Anamnesis.Application.Contracts;
using Anamnesis.Domain.Entidades;

namespace Anamnesis.Tray;

internal sealed class BandejaNotificadorLembrete(NotifyIcon icone) : INotificadorLembrete
{
    public Task NotificarAsync(
        LembreteTarefa lembrete,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        icone.ShowBalloonTip(
            8000,
            "Lembrete de tarefa",
            lembrete.DescricaoTarefa,
            ToolTipIcon.Info);
        return Task.CompletedTask;
    }
}

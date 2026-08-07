namespace Anamnesis.Tray;

internal enum TipoNotificacaoDesktop
{
    ProcessamentoConcluido,
    Falha
}

internal sealed record NotificacaoDesktop(
    TipoNotificacaoDesktop Tipo,
    Guid ReuniaoId,
    string Titulo,
    string Mensagem);

internal sealed class NotificacoesDesktopState
{
    private readonly Dictionary<Guid, string> _estados = [];
    private bool _inicializado;

    public IReadOnlyList<NotificacaoDesktop> Observar(
        IReadOnlyList<ReuniaoDesktopPoc> reunioes)
    {
        ArgumentNullException.ThrowIfNull(reunioes);
        if (!_inicializado)
        {
            foreach (var reuniao in reunioes)
            {
                _estados[reuniao.Id] = reuniao.Status;
            }

            _inicializado = true;
            return [];
        }

        var notificacoes = new List<NotificacaoDesktop>();
        var idsAtuais = reunioes.Select(reuniao => reuniao.Id).ToHashSet();
        foreach (var reuniao in reunioes)
        {
            _estados.TryGetValue(reuniao.Id, out var estadoAnterior);
            if (!string.Equals(estadoAnterior, reuniao.Status, StringComparison.Ordinal))
            {
                var notificacao = CriarNotificacao(reuniao);
                if (notificacao is not null)
                {
                    notificacoes.Add(notificacao);
                }
            }

            _estados[reuniao.Id] = reuniao.Status;
        }

        foreach (var idRemovido in _estados.Keys.Where(id => !idsAtuais.Contains(id)).ToArray())
        {
            _estados.Remove(idRemovido);
        }

        return notificacoes;
    }

    private static NotificacaoDesktop? CriarNotificacao(ReuniaoDesktopPoc reuniao)
    {
        if (reuniao.Status is "Ata pronta" or "Retenção pendente" or "Gravação removida")
        {
            return new NotificacaoDesktop(
                TipoNotificacaoDesktop.ProcessamentoConcluido,
                reuniao.Id,
                "Reunião concluída",
                $"{reuniao.Titulo}: transcrição e ata estão prontas.");
        }

        if (reuniao.Status == "Falha")
        {
            return new NotificacaoDesktop(
                TipoNotificacaoDesktop.Falha,
                reuniao.Id,
                "Falha no processamento",
                $"{reuniao.Titulo}: abra o Anamnesis para ver o diagnóstico e corrigir.");
        }

        return null;
    }
}

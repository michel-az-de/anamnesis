namespace Anamnesis.Tray;

internal sealed record TrayMenuState(
    string Estado,
    bool PodeIniciar,
    bool PodeEncerrar,
    string TextoPendencias)
{
    public static async Task<TrayMenuState> AtualizarAsync(
        IDesktopSession sessao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        await sessao.AtualizarAsync(cancellationToken);
        return Criar(
            sessao.Etapa,
            sessao.RecuperacaoPendente,
            sessao.JobsNaFila);
    }

    public static TrayMenuState Criar(
        EtapaDesktopPoc etapa,
        bool recuperacaoPendente,
        int pendencias)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pendencias);
        var estado = recuperacaoPendente
            ? "Recuperação pendente"
            : etapa switch
            {
                EtapaDesktopPoc.Gravando => "Gravando",
                EtapaDesktopPoc.Processando => "Processando localmente",
                _ => "Pronto"
            };
        var textoPendencias = pendencias > 0
            ? $"Processar pendências ({pendencias})"
            : "Processar pendências";

        return new TrayMenuState(
            estado,
            PodeIniciar: etapa != EtapaDesktopPoc.Gravando && !recuperacaoPendente,
            PodeEncerrar: etapa == EtapaDesktopPoc.Gravando,
            textoPendencias);
    }
}

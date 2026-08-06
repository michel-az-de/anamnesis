using Anamnesis.Application.Modelos;

namespace Anamnesis.Tray;

internal enum AcaoSugestaoDeteccao
{
    Iniciar,
    Ignorar,
    Silenciar,
    Fechar
}

internal interface IDeteccaoPrompt : IDisposable
{
    Task<AcaoSugestaoDeteccao> SugerirInicioAsync(
        PlataformaLocal plataforma,
        CancellationToken cancellationToken);

    bool ConsumirCancelamentoInicio();

    void MostrarContagemInicio(PlataformaLocal plataforma, TimeSpan restante);

    void FecharContagemInicio();

    bool ConsumirCancelamentoEncerramento();

    void MostrarAvisoEncerramento(TimeSpan restante);

    void FecharAvisoEncerramento();

    void NotificarInicioAutomatico();

    void MostrarFalhaSegura();

    void Fechar();
}

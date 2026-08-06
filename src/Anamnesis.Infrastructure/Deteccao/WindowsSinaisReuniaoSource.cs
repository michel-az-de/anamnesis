using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Infrastructure.Configuracao;

namespace Anamnesis.Infrastructure.Deteccao;

internal interface IWindowsSinaisProbe
{
    IReadOnlyCollection<string> ListarFamiliasComCaptura();

    IReadOnlyCollection<string> ListarFamiliasComRenderizacao();

    IReadOnlyCollection<JanelaWindows> ListarJanelas();
}

internal sealed class WindowsSinaisLeituraParcialException<T>(
    IReadOnlyCollection<T> resultados) : Exception("A leitura local foi concluída parcialmente.")
{
    public IReadOnlyCollection<T> Resultados { get; } = resultados;
}

public sealed class WindowsSinaisReuniaoSource : ISinaisReuniaoSource
{
    private readonly IWindowsSinaisProbe _probe;
    private readonly ClassificadorSinaisWindows _classificador;

    public WindowsSinaisReuniaoSource(DeteccaoLocalOptions options)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            throw new PlatformNotSupportedException("A detecção local exige Windows 7 ou posterior.");
        }

        _probe = new WindowsSinaisProbe();
        _classificador = new ClassificadorSinaisWindows(options);
    }

    internal WindowsSinaisReuniaoSource(
        IWindowsSinaisProbe probe,
        ClassificadorSinaisWindows classificador)
    {
        _probe = probe;
        _classificador = classificador;
    }

    public Task<SinaisDeteccaoReuniao> ObterAsync(CancellationToken cancellationToken) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var capturas = Tentar(_probe.ListarFamiliasComCaptura, out var capturasValidas);
                var renderizacoes = Tentar(
                    _probe.ListarFamiliasComRenderizacao,
                    out var renderizacoesValidas);
                var janelas = Tentar(_probe.ListarJanelas, out var janelasValidas);
                var sinais = _classificador.Classificar(new LeituraSinaisWindows(
                    capturas,
                    renderizacoes,
                    janelas));
                return sinais with
                {
                    ColetaConfiavel = capturasValidas && renderizacoesValidas && janelasValidas
                };
            },
            cancellationToken);

    private static IReadOnlyCollection<T> Tentar<T>(
        Func<IReadOnlyCollection<T>> operacao,
        out bool sucesso)
    {
        try
        {
            var resultado = operacao();
            sucesso = true;
            return resultado;
        }
        catch (WindowsSinaisLeituraParcialException<T> exception)
        {
            sucesso = false;
            return exception.Resultados;
        }
        catch (Exception) when (!Environment.HasShutdownStarted)
        {
            sucesso = false;
            return [];
        }
    }
}

using System.Text.Json;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;

namespace Anamnesis.Tray;

internal static class DiagnosticoDeteccaoRunner
{
    public static async Task<int> ExecutarAsync(
        ISinaisReuniaoSource fonte,
        ModoDeteccaoReuniao modo,
        DiagnosticoDeteccaoOptions options,
        TextWriter saida,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(saida);

        await EscreverAsync(saida, new
        {
            evento = "diagnostico_deteccao_iniciado",
            modo = modo.ToString(),
            amostras = options.Amostras,
            intervaloMs = options.Intervalo.TotalMilliseconds
        }, cancellationToken);

        for (var indice = 1; indice <= options.Amostras; indice++)
        {
            var sinais = await fonte.ObterAsync(cancellationToken);
            await EscreverAsync(saida, new
            {
                evento = "sinais_locais",
                amostra = indice,
                utc = DateTimeOffset.UtcNow,
                microfoneAtivo = sinais.MicrofoneAtivo,
                audioSaidaAtivo = sinais.AudioSaidaAtivo,
                plataformaCodigo = sinais.Plataforma?.Chave,
                plataformaNome = sinais.Plataforma?.Nome,
                ambiguo = sinais.Ambiguo,
                coletaConfiavel = sinais.ColetaConfiavel
            }, cancellationToken);

            if (indice < options.Amostras)
            {
                await Task.Delay(options.Intervalo, cancellationToken);
            }
        }

        await EscreverAsync(saida, new
        {
            evento = "diagnostico_deteccao_concluido"
        }, cancellationToken);
        return 0;
    }

    private static async Task EscreverAsync(
        TextWriter saida,
        object registro,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(registro);
        await saida.WriteLineAsync(json.AsMemory(), cancellationToken);
        await saida.FlushAsync(cancellationToken);
    }
}

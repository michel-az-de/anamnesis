using System.Globalization;

namespace Anamnesis.Worker;

public sealed record ModoRetencaoWorkerOptions(Guid ReuniaoId, DateTimeOffset Agora, bool Aplicar)
{
    public static ModoRetencaoWorkerOptions? Interpretar(string[] argumentos)
    {
        var simular = argumentos.Contains("--retencao-simular", StringComparer.Ordinal);
        var aplicar = argumentos.Contains("--retencao-aplicar", StringComparer.Ordinal);
        if (!simular && !aplicar)
        {
            return null;
        }

        if (simular && aplicar)
        {
            throw new ArgumentException("Informe somente --retencao-simular ou --retencao-aplicar.");
        }

        var reuniaoTexto = ObterValor(argumentos, "--reuniao");
        if (!Guid.TryParse(reuniaoTexto, out var reuniaoId))
        {
            throw new ArgumentException("O valor de --reuniao deve ser um GUID válido.");
        }

        var agoraTexto = ObterValor(argumentos, "--agora");
        if (!DateTimeOffset.TryParse(
                agoraTexto,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var agora))
        {
            throw new ArgumentException("O valor de --agora deve ser um instante ISO 8601 válido.");
        }

        if (aplicar && !argumentos.Contains("--confirmar-lixeira", StringComparer.Ordinal))
        {
            throw new ArgumentException("A aplicação exige --confirmar-lixeira.");
        }

        return new ModoRetencaoWorkerOptions(reuniaoId, agora, aplicar);
    }

    private static string ObterValor(string[] argumentos, string nome)
    {
        var indice = Array.IndexOf(argumentos, nome);
        if (indice < 0 || indice + 1 >= argumentos.Length || argumentos[indice + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Informe {nome} com um valor.");
        }

        return argumentos[indice + 1];
    }
}

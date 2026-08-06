namespace Anamnesis.Tray;

internal sealed record DiagnosticoDeteccaoOptions(
    int Amostras,
    TimeSpan Intervalo,
    string? CaminhoSaida = null)
{
    private const string Argumento = "--diagnostico-deteccao";

    public static DiagnosticoDeteccaoOptions? Obter(IReadOnlyList<string> argumentos)
    {
        if (!argumentos.Any(argumento =>
                string.Equals(argumento, Argumento, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var amostras = LerInteiro(argumentos, "--amostras", 5);
        var intervaloMs = LerInteiro(argumentos, "--intervalo-ms", 1_000);
        return new DiagnosticoDeteccaoOptions(
            Math.Clamp(amostras, 1, 60),
            TimeSpan.FromMilliseconds(Math.Clamp(intervaloMs, 100, 5_000)),
            LerTexto(argumentos, "--saida"));
    }

    private static int LerInteiro(
        IReadOnlyList<string> argumentos,
        string nome,
        int padrao)
    {
        for (var indice = 0; indice < argumentos.Count - 1; indice++)
        {
            if (string.Equals(argumentos[indice], nome, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(argumentos[indice + 1], out var valor))
            {
                return valor;
            }
        }

        return padrao;
    }

    private static string? LerTexto(
        IReadOnlyList<string> argumentos,
        string nome)
    {
        for (var indice = 0; indice < argumentos.Count - 1; indice++)
        {
            if (string.Equals(argumentos[indice], nome, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(argumentos[indice + 1]))
            {
                return argumentos[indice + 1];
            }
        }

        return null;
    }
}

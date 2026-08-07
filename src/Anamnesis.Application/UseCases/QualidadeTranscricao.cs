using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.UseCases;

public static class QualidadeTranscricao
{
    private const double ProporcaoLinhaDominante = 0.20;
    private const double ProporcaoMinimaConfiavel = 0.20;
    private static readonly HashSet<string> MarcadoresNaoVerbais = new(StringComparer.OrdinalIgnoreCase)
    {
        "música",
        "musica",
        "música de fundo",
        "musica de fundo",
        "music",
        "background music",
        "silêncio",
        "silencio",
        "silence",
        "aplausos",
        "applause"
    };

    public static TranscricaoGerada LimparEValidar(TranscricaoGerada transcricao)
    {
        ArgumentNullException.ThrowIfNull(transcricao);
        var linhas = transcricao.Texto
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(linha => !string.IsNullOrWhiteSpace(linha))
            .ToArray();
        if (linhas.Length == 0)
        {
            throw new TranscricaoBaixaQualidadeException();
        }

        var normalizadas = linhas.Select(Normalizar).ToArray();
        var dominantes = normalizadas
            .GroupBy(linha => linha, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() >= 3 && grupo.Count() / (double)linhas.Length >= ProporcaoLinhaDominante)
            .Select(grupo => grupo.Key)
            .ToHashSet(StringComparer.Ordinal);
        var confiaveis = linhas
            .Where((linha, indice) =>
                !EhMarcadorNaoVerbal(normalizadas[indice]) &&
                !EhLinhaDegenerada(linha) &&
                !dominantes.Contains(normalizadas[indice]))
            .ToArray();

        if (confiaveis.Length == 0 ||
            linhas.Length >= 5 && confiaveis.Length / (double)linhas.Length < ProporcaoMinimaConfiavel)
        {
            throw new TranscricaoBaixaQualidadeException();
        }

        return transcricao with { Texto = string.Join(Environment.NewLine, confiaveis).Trim() };
    }

    private static bool EhMarcadorNaoVerbal(string linhaNormalizada) =>
        MarcadoresNaoVerbais.Contains(linhaNormalizada);

    private static bool EhLinhaDegenerada(string linha)
    {
        var palavras = linha
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalizar)
            .Where(palavra => palavra.Length > 0)
            .ToArray();
        return palavras.Length >= 12 &&
               palavras.GroupBy(palavra => palavra, StringComparer.Ordinal).Max(grupo => grupo.Count()) /
               (double)palavras.Length >= 0.60;
    }

    private static string Normalizar(string valor) =>
        valor.Trim().Trim('[', ']', '(', ')', '{', '}', '.', ',', ':', ';', '!', '?', '♪', ' ').ToLowerInvariant();
}

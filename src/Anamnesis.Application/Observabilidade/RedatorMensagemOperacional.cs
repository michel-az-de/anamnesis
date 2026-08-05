using System.Text.RegularExpressions;

namespace Anamnesis.Application.Observabilidade;

public static class RedatorMensagemOperacional
{
    private const int LimiteMensagem = 512;
    private const int LimiteEntrada = 4096;
    private static readonly TimeSpan LimiteRegex = TimeSpan.FromMilliseconds(100);
    private static readonly Regex Segredo = new(
        @"\b(senha|password|token|secret|authorization)\s*[:=]\s*[^\s;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        LimiteRegex);
    private static readonly Regex Bearer = new(
        @"\bbearer\s+[^\s;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        LimiteRegex);
    private static readonly Regex CaminhoWindows = new(
        @"(?:[a-z]:\\|\\\\)[^\s\""']+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        LimiteRegex);

    public static string Redigir(string? mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
        {
            return "Operação local registrada.";
        }

        var entrada = mensagem.Length > LimiteEntrada ? mensagem[..LimiteEntrada] : mensagem;
        entrada = entrada.Replace('\r', ' ').Replace('\n', ' ');
        entrada = Segredo.Replace(entrada, "$1=[REMOVIDO]");
        entrada = Bearer.Replace(entrada, "Bearer [REMOVIDO]");
        entrada = CaminhoWindows.Replace(entrada, "[CAMINHO_REMOVIDO]");
        entrada = string.Join(' ', entrada.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return entrada.Length <= LimiteMensagem ? entrada : entrada[..LimiteMensagem];
    }
}

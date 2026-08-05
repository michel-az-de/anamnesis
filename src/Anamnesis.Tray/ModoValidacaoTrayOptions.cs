namespace Anamnesis.Tray;

public sealed record ModoValidacaoTrayOptions(TimeSpan Duracao)
{
    private const string Argumento = "--gravar-teste-segundos";

    public static ModoValidacaoTrayOptions? Obter(IReadOnlyList<string> argumentos)
    {
        var indice = argumentos.ToList().FindIndex(argumento =>
            string.Equals(argumento, Argumento, StringComparison.OrdinalIgnoreCase));
        if (indice < 0)
        {
            return null;
        }

        if (indice + 1 >= argumentos.Count ||
            !int.TryParse(argumentos[indice + 1], out var segundos) ||
            segundos <= 0)
        {
            throw new ArgumentException("A duração da gravação de teste deve ser um número inteiro positivo de segundos.");
        }

        return new ModoValidacaoTrayOptions(TimeSpan.FromSeconds(segundos));
    }
}

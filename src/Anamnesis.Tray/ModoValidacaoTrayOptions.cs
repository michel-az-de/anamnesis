namespace Anamnesis.Tray;

public sealed record ModoValidacaoTrayOptions(TimeSpan Duracao, bool IniciarWorker)
{
    private const string Argumento = "--gravar-teste-segundos";
    private const string ArgumentoIniciarWorker = "--iniciar-worker";

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

        var iniciarWorker = argumentos.Any(argumento =>
            string.Equals(argumento, ArgumentoIniciarWorker, StringComparison.OrdinalIgnoreCase));
        return new ModoValidacaoTrayOptions(TimeSpan.FromSeconds(segundos), iniciarWorker);
    }
}

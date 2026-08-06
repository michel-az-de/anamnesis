namespace Anamnesis.Tray;

internal static class IconeAnamnesis
{
    private const string NomeRecurso = "Anamnesis.Tray.Assets.Anamnesis.ico";

    public static Icon Carregar()
    {
        using var stream = AbrirStream();
        using var origem = new Icon(stream);
        return (Icon)origem.Clone();
    }

    internal static byte[] ObterBytes()
    {
        using var stream = AbrirStream();
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    }

    private static Stream AbrirStream() =>
        typeof(IconeAnamnesis).Assembly.GetManifestResourceStream(NomeRecurso)
        ?? throw new InvalidOperationException("O icone embarcado do Anamnesis nao foi encontrado.");
}

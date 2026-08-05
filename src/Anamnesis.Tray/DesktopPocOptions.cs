namespace Anamnesis.Tray;

public static class DesktopPocOptions
{
    private const string Argumento = "--poc-desktop";

    public static bool EstaAtivo(IReadOnlyList<string> argumentos) =>
        argumentos.Any(argumento => string.Equals(argumento, Argumento, StringComparison.OrdinalIgnoreCase));
}


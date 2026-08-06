using Microsoft.Win32;

namespace Anamnesis.Tray;

internal sealed class InicializacaoWindows
{
    private const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string NomeValor = "Anamnesis";
    private readonly string _comando;
    private readonly Func<string?> _ler;
    private readonly Action<string?> _gravar;

    public InicializacaoWindows(string caminhoExecutavel)
        : this(caminhoExecutavel, LerRegistro, GravarRegistro)
    {
    }

    internal InicializacaoWindows(
        string caminhoExecutavel,
        Func<string?> ler,
        Action<string?> gravar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoExecutavel);
        _comando = $"\"{Path.GetFullPath(caminhoExecutavel)}\" --background";
        _ler = ler;
        _gravar = gravar;
    }

    public bool EstaAtiva => string.Equals(_ler(), _comando, StringComparison.OrdinalIgnoreCase);

    public void Ativar() => _gravar(_comando);

    public void Desativar() => _gravar(null);

    private static string? LerRegistro()
    {
        using var chave = Registry.CurrentUser.OpenSubKey(ChaveRun, writable: false);
        return chave?.GetValue(NomeValor) as string;
    }

    private static void GravarRegistro(string? comando)
    {
        using var chave = Registry.CurrentUser.CreateSubKey(ChaveRun, writable: true);
        if (comando is null)
        {
            chave.DeleteValue(NomeValor, throwOnMissingValue: false);
            return;
        }

        chave.SetValue(NomeValor, comando, RegistryValueKind.String);
    }
}

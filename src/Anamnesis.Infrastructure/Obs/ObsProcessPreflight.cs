using System.Diagnostics;
using System.Net.Sockets;
using Anamnesis.Application.Contracts;

namespace Anamnesis.Infrastructure.Obs;

public sealed class ObsProcessPreflight : IObsPreflight
{
    private const int MaximoTentativasPadrao = 120;
    private static readonly TimeSpan IntervaloPadrao = TimeSpan.FromMilliseconds(250);
    private readonly Uri _endereco;
    private readonly string _caminhoExecutavel;
    private readonly Func<Uri, CancellationToken, Task<bool>> _verificarDisponibilidade;
    private readonly Action<ProcessStartInfo> _iniciarProcesso;
    private readonly int _maximoTentativas;
    private readonly TimeSpan _intervalo;

    public ObsProcessPreflight(Uri endereco, string caminhoExecutavel)
        : this(
            endereco,
            caminhoExecutavel,
            VerificarDisponibilidadeAsync,
            IniciarProcesso,
            MaximoTentativasPadrao,
            IntervaloPadrao)
    {
    }

    internal ObsProcessPreflight(
        Uri endereco,
        string caminhoExecutavel,
        Func<Uri, CancellationToken, Task<bool>> verificarDisponibilidade,
        Action<ProcessStartInfo> iniciarProcesso,
        int maximoTentativas,
        TimeSpan intervalo)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximoTentativas);
        _endereco = endereco;
        _caminhoExecutavel = caminhoExecutavel;
        _verificarDisponibilidade = verificarDisponibilidade;
        _iniciarProcesso = iniciarProcesso;
        _maximoTentativas = maximoTentativas;
        _intervalo = intervalo;
    }

    public async Task PrepararAsync(CancellationToken cancellationToken)
    {
        if (await _verificarDisponibilidade(_endereco, cancellationToken))
        {
            return;
        }

        _iniciarProcesso(CriarProcessStartInfo());
        for (var tentativa = 0; tentativa < _maximoTentativas; tentativa++)
        {
            await Task.Delay(_intervalo, cancellationToken);
            if (await _verificarDisponibilidade(_endereco, cancellationToken))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"O websocket do OBS não ficou disponível em {_endereco} após 30 segundos. " +
            "Confirme se o servidor WebSocket está ativado no OBS.");
    }

    internal ProcessStartInfo CriarProcessStartInfo()
    {
        var executavel = Path.GetFullPath(_caminhoExecutavel);
        if (!File.Exists(executavel))
        {
            throw new FileNotFoundException(
                "O executável do OBS Studio não foi encontrado. Configure CaminhoExecutavelObs.",
                executavel);
        }

        var inicio = new ProcessStartInfo(executavel)
        {
            WorkingDirectory = Path.GetDirectoryName(executavel)!,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized
        };
        inicio.ArgumentList.Add("--minimize-to-tray");
        return inicio;
    }

    public static string ResolverCaminhoExecutavel(string? caminhoConfigurado)
    {
        if (!string.IsNullOrWhiteSpace(caminhoConfigurado))
        {
            return Path.GetFullPath(caminhoConfigurado);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "obs-studio",
            "bin",
            "64bit",
            "obs64.exe");
    }

    private static async Task<bool> VerificarDisponibilidadeAsync(
        Uri endereco,
        CancellationToken cancellationToken)
    {
        using var cliente = new TcpClient();
        try
        {
            await cliente.ConnectAsync(endereco.Host, endereco.Port, cancellationToken);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static void IniciarProcesso(ProcessStartInfo inicio)
    {
        using var processo = Process.Start(inicio)
            ?? throw new InvalidOperationException("O Windows não retornou o processo do OBS Studio.");
    }
}

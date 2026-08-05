using System.Diagnostics;
using Anamnesis.Infrastructure.Processos;

namespace Anamnesis.Infrastructure.Whisper;

public sealed class DockerProcessPreflight : IDockerPreflight
{
    private const int MaximoTentativasPadrao = 60;
    private static readonly TimeSpan IntervaloPadrao = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TimeoutVerificacaoPadrao = TimeSpan.FromSeconds(15);
    private readonly string _caminhoDockerDesktop;
    private readonly Func<CancellationToken, Task<bool>> _verificarEngine;
    private readonly Action<ProcessStartInfo> _iniciarProcesso;
    private readonly int _maximoTentativas;
    private readonly TimeSpan _intervalo;
    private readonly TimeSpan _timeoutVerificacao;

    public DockerProcessPreflight(string caminhoDockerCli, string caminhoDockerDesktop)
        : this(
            caminhoDockerCli,
            caminhoDockerDesktop,
            cancellationToken => VerificarEngineAsync(caminhoDockerCli, cancellationToken),
            IniciarProcesso,
            MaximoTentativasPadrao,
            IntervaloPadrao,
            TimeoutVerificacaoPadrao)
    {
    }

    internal DockerProcessPreflight(
        string caminhoDockerCli,
        string caminhoDockerDesktop,
        Func<CancellationToken, Task<bool>> verificarEngine,
        Action<ProcessStartInfo> iniciarProcesso,
        int maximoTentativas,
        TimeSpan intervalo,
        TimeSpan? timeoutVerificacao = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoDockerCli);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximoTentativas);
        _caminhoDockerDesktop = caminhoDockerDesktop;
        _verificarEngine = verificarEngine;
        _iniciarProcesso = iniciarProcesso;
        _maximoTentativas = maximoTentativas;
        _intervalo = intervalo;
        _timeoutVerificacao = timeoutVerificacao ?? TimeoutVerificacaoPadrao;
    }

    public async Task PrepararAsync(CancellationToken cancellationToken)
    {
        if (await VerificarEngineComDeadlineAsync(cancellationToken))
        {
            return;
        }

        _iniciarProcesso(CriarProcessStartInfo());
        for (var tentativa = 0; tentativa < _maximoTentativas; tentativa++)
        {
            await Task.Delay(_intervalo, cancellationToken);
            if (await VerificarEngineComDeadlineAsync(cancellationToken))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"O engine do Docker não ficou disponível após {_maximoTentativas} tentativas " +
            $"({(_intervalo * _maximoTentativas).TotalSeconds:0} segundos). " +
            "Abra Docker Desktop e confirme licença, atualização ou erro de inicialização.");
    }

    internal ProcessStartInfo CriarProcessStartInfo()
    {
        var executavel = Path.GetFullPath(_caminhoDockerDesktop);
        if (!File.Exists(executavel))
        {
            throw new FileNotFoundException(
                "O executável do Docker Desktop não foi encontrado. Configure CaminhoExecutavelDockerDesktop.",
                executavel);
        }

        return new ProcessStartInfo(executavel)
        {
            WorkingDirectory = Path.GetDirectoryName(executavel)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
    }

    public static string ResolverCaminhoExecutavel(string? caminhoConfigurado)
    {
        if (!string.IsNullOrWhiteSpace(caminhoConfigurado))
        {
            return Path.GetFullPath(caminhoConfigurado);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Docker",
            "Docker",
            "Docker Desktop.exe");
    }

    private static async Task<bool> VerificarEngineAsync(
        string caminhoDockerCli,
        CancellationToken cancellationToken)
    {
        var executavel = Path.GetFullPath(caminhoDockerCli);
        if (!File.Exists(executavel))
        {
            throw new FileNotFoundException("O executável do Docker CLI não foi encontrado.", executavel);
        }

        var inicio = new ProcessStartInfo(executavel)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        inicio.ArgumentList.Add("info");
        inicio.ArgumentList.Add("--format");
        inicio.ArgumentList.Add("{{.ServerVersion}}");
        using var processo = Process.Start(inicio)
            ?? throw new InvalidOperationException("O Windows não retornou o processo do Docker CLI.");
        var saida = processo.StandardOutput.ReadToEndAsync(cancellationToken);
        var erro = processo.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await processo.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(saida, erro);
            return processo.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            await ProcessoExterno.EncerrarAsync(processo);
            await ProcessoExterno.ObservarSemSubstituirErroAsync(saida, erro);
            throw;
        }
    }

    private async Task<bool> VerificarEngineComDeadlineAsync(CancellationToken cancellationToken)
    {
        using var deadline = ProcessoExterno.CriarDeadline(_timeoutVerificacao, cancellationToken);
        try
        {
            return await _verificarEngine(deadline.Token).WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException excecao)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw ProcessoExterno.CriarTimeout("A verificação do Docker", _timeoutVerificacao, excecao);
        }
    }

    private static void IniciarProcesso(ProcessStartInfo inicio)
    {
        using var processo = Process.Start(inicio)
            ?? throw new InvalidOperationException("O Windows não retornou o processo do Docker Desktop.");
    }
}

using System.Diagnostics;
using Anamnesis.Application.Contracts;

namespace Anamnesis.Infrastructure.Processos;

public sealed class WorkerProcessLauncher(
    string caminhoExecutavel,
    string caminhoConfiguracao) : IWorkerLauncher
{
    private const string NomeVariavelConfiguracao = "ANAMNESIS_CONFIGURACAO";

    public Task IniciarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var processo = Process.Start(CriarProcessStartInfo())
            ?? throw new InvalidOperationException("O Windows não retornou o processo do Worker.");
        return Task.CompletedTask;
    }

    internal ProcessStartInfo CriarProcessStartInfo()
    {
        var executavel = Path.GetFullPath(caminhoExecutavel);
        if (!File.Exists(executavel))
        {
            throw new FileNotFoundException("O executável do Worker não foi encontrado.", executavel);
        }

        var inicio = new ProcessStartInfo(executavel)
        {
            WorkingDirectory = Path.GetDirectoryName(executavel)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        inicio.Environment[NomeVariavelConfiguracao] = Path.GetFullPath(caminhoConfiguracao);
        return inicio;
    }

    public static string ResolverCaminhoExecutavel(
        string diretorioTray,
        string? caminhoSobrescrito)
    {
        if (!string.IsNullOrWhiteSpace(caminhoSobrescrito))
        {
            return Path.GetFullPath(caminhoSobrescrito);
        }

        return Path.GetFullPath(Path.Combine(
            diretorioTray,
            "..",
            "worker",
            "Anamnesis.Worker.exe"));
    }
}

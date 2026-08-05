using Anamnesis.Infrastructure.Processos;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WorkerProcessLauncherTests : IDisposable
{
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-worker-launcher-{Guid.NewGuid():N}");

    public WorkerProcessLauncherTests() => Directory.CreateDirectory(_diretorio);

    [Fact]
    public void DeveResolverWorkerNoLayoutInstalado()
    {
        var diretorioTray = Path.Combine(_diretorio, "tray");
        var caminhoWorker = Path.Combine(_diretorio, "worker", "Anamnesis.Worker.exe");
        Directory.CreateDirectory(diretorioTray);
        Directory.CreateDirectory(Path.GetDirectoryName(caminhoWorker)!);
        File.WriteAllText(caminhoWorker, "fake");

        var resolvido = WorkerProcessLauncher.ResolverCaminhoExecutavel(diretorioTray, null);

        Assert.Equal(caminhoWorker, resolvido);
    }

    [Fact]
    public void DevePrepararProcessoOcultoComMesmaConfiguracao()
    {
        var caminhoWorker = Path.Combine(_diretorio, "worker.exe");
        var caminhoConfiguracao = Path.Combine(_diretorio, "config.json");
        File.WriteAllText(caminhoWorker, "fake");
        File.WriteAllText(caminhoConfiguracao, "{}");
        var launcher = new WorkerProcessLauncher(caminhoWorker, caminhoConfiguracao);

        var inicio = launcher.CriarProcessStartInfo();

        Assert.Equal(caminhoWorker, inicio.FileName);
        Assert.Equal(_diretorio, inicio.WorkingDirectory);
        Assert.False(inicio.UseShellExecute);
        Assert.True(inicio.CreateNoWindow);
        Assert.Equal(caminhoConfiguracao, inicio.Environment["ANAMNESIS_CONFIGURACAO"]);
    }

    public void Dispose() => Directory.Delete(_diretorio, recursive: true);
}

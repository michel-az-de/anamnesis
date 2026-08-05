using Anamnesis.Infrastructure.Configuracao;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class ArquivoConfiguracaoTests : IAsyncLifetime
{
    private readonly string _diretorio = Path.Combine(Path.GetTempPath(), $"anamnesis-config-{Guid.NewGuid():N}");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (Directory.Exists(_diretorio))
        {
            Directory.Delete(_diretorio, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task DeveCriarECarregarConfiguracaoPadrao()
    {
        var caminhoArquivo = Path.Combine(_diretorio, "config.json");
        var arquivo = new ArquivoConfiguracao(caminhoArquivo);

        var criada = await arquivo.CarregarAsync(CancellationToken.None);
        var carregada = await arquivo.CarregarAsync(CancellationToken.None);

        Assert.True(File.Exists(caminhoArquivo));
        Assert.Equal("ws://127.0.0.1:4455", criada.EnderecoObs);
        Assert.Equal(criada.CaminhoBanco, carregada.CaminhoBanco);
        Assert.Equal(criada.DiretorioArquivo, carregada.DiretorioArquivo);
        Assert.Equal("pt", carregada.IdiomaWhisper);
        Assert.Empty(carregada.ArgumentosCli);
    }
}

using Anamnesis.Application.Modelos;
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
    public async Task ConfiguracaoLegadaDeveHerdarDeteccaoAssistidaSegura()
    {
        var caminho = Path.Combine(_diretorio, "config-legada.json");
        Directory.CreateDirectory(_diretorio);
        await File.WriteAllTextAsync(
            caminho,
            """
            {
              "CaminhoBanco": "dados/anamnesis.db",
              "DiretorioArquivo": "dados/arquivo"
            }
            """);

        var configuracao = await new ArquivoConfiguracao(caminho)
            .CarregarAsync(CancellationToken.None);

        Assert.Equal(ModoDeteccaoReuniao.Assistido, configuracao.Deteccao.Modo);
        Assert.False(configuracao.Deteccao.FinalizacaoAutomaticaAtiva);
        Assert.Contains(
            configuracao.Deteccao.Aplicativos,
            aplicativo => aplicativo.Chave == "native_teams");
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
        Assert.Equal(14, carregada.RetencaoEventosDias);
        Assert.Empty(carregada.ArgumentosCli);
    }

    [Fact]
    public async Task NaoDeveGravarASenhaDoObsEmTextoClaro()
    {
        var caminhoArquivo = Path.Combine(_diretorio, "config.json");
        await new ArquivoConfiguracao(caminhoArquivo).SalvarAsync(
            ConfiguracaoAnamnesis.CriarPadrao() with { SenhaObs = "senha-secreta-do-obs" },
            CancellationToken.None);

        var conteudo = await File.ReadAllTextAsync(caminhoArquivo);

        Assert.DoesNotContain("senha-secreta-do-obs", conteudo, StringComparison.Ordinal);
        Assert.Contains("dpapi:", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeveRecuperarASenhaDoObsProtegida()
    {
        var caminhoArquivo = Path.Combine(_diretorio, "config.json");
        var arquivo = new ArquivoConfiguracao(caminhoArquivo);
        await arquivo.SalvarAsync(
            ConfiguracaoAnamnesis.CriarPadrao() with { SenhaObs = "senha-secreta-do-obs" },
            CancellationToken.None);

        var carregada = await arquivo.CarregarAsync(CancellationToken.None);

        Assert.Equal("senha-secreta-do-obs", carregada.SenhaObs);
    }

    [Fact]
    public async Task DeveMigrarSenhaLegadaEmTextoClaro()
    {
        var caminhoArquivo = Path.Combine(_diretorio, "config.json");
        Directory.CreateDirectory(_diretorio);
        await File.WriteAllTextAsync(
            caminhoArquivo,
            """{"CaminhoBanco":"C:\\a.db","DiretorioArquivo":"C:\\a","SenhaObs":"senha-legada"}""");
        var arquivo = new ArquivoConfiguracao(caminhoArquivo);

        // Uma configuração anterior à proteção continua carregando.
        var legada = await arquivo.CarregarAsync(CancellationToken.None);
        Assert.Equal("senha-legada", legada.SenhaObs);

        // E é reescrita protegida no salvamento seguinte, sem intervenção do usuário.
        await arquivo.SalvarAsync(legada, CancellationToken.None);

        Assert.DoesNotContain(
            "senha-legada",
            await File.ReadAllTextAsync(caminhoArquivo),
            StringComparison.Ordinal);
        Assert.Equal("senha-legada", (await arquivo.CarregarAsync(CancellationToken.None)).SenhaObs);
    }
}

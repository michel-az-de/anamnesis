using Anamnesis.Application.Modelos;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Deteccao;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class ClassificadorSinaisWindowsTests
{
    private readonly ClassificadorSinaisWindows _classificador =
        new(DeteccaoLocalOptions.Padrao);

    [Fact]
    public void AplicativoNativoDeveExigirSessaoDeAudioDaFamilia()
    {
        var semSessao = _classificador.Classificar(new LeituraSinaisWindows(
            [],
            [],
            [new JanelaWindows("ms-teams", "Microsoft Teams")]));
        var comCaptura = _classificador.Classificar(new LeituraSinaisWindows(
            ["ms-teams"],
            ["ms-teams"],
            [new JanelaWindows("ms-teams", "Microsoft Teams")]));

        Assert.Null(semSessao.Plataforma);
        Assert.False(semSessao.MicrofoneAtivo);
        Assert.NotNull(comCaptura.Plataforma);
        Assert.Equal("native_teams", comCaptura.Plataforma!.Chave);
        Assert.True(comCaptura.MicrofoneAtivo);
        Assert.True(comCaptura.AudioSaidaAtivo);
    }

    [Fact]
    public void NavegadorGenericoNuncaDeveIdentificarChamadaSemAssinatura()
    {
        var sinais = _classificador.Classificar(new LeituraSinaisWindows(
            ["chrome"],
            ["chrome"],
            [new JanelaWindows("chrome", "YouTube - Google Chrome")]));

        Assert.True(sinais.MicrofoneAtivo);
        Assert.True(sinais.AudioSaidaAtivo);
        Assert.Null(sinais.Plataforma);
        Assert.False(sinais.Ambiguo);
    }

    [Fact]
    public void NavegadorComCapturaEJanelaConhecidaDeveIdentificarMeet()
    {
        var sinais = _classificador.Classificar(new LeituraSinaisWindows(
            ["chrome"],
            ["chrome"],
            [new JanelaWindows("chrome", "Meet - abc-defg-hij - Google Chrome")]));

        Assert.Equal("browser_meet", sinais.Plataforma!.Chave);
        Assert.Equal("Google Meet", sinais.Plataforma.Nome);
        Assert.Equal(OrigemPlataformaLocal.Navegador, sinais.Plataforma.Origem);
        Assert.True(sinais.MicrofoneAtivo);
    }

    [Fact]
    public void RenderizacaoSemCapturaPodeSomenteAlimentarModoAssistido()
    {
        var sinais = _classificador.Classificar(new LeituraSinaisWindows(
            [],
            ["zoom"],
            []));

        Assert.Equal("native_zoom", sinais.Plataforma!.Chave);
        Assert.False(sinais.MicrofoneAtivo);
        Assert.True(sinais.AudioSaidaAtivo);
    }

    [Fact]
    public void DuasPlataformasSimultaneasDevemProduzirSinalAmbiguo()
    {
        var sinais = _classificador.Classificar(new LeituraSinaisWindows(
            ["ms-teams", "zoom"],
            ["ms-teams", "zoom"],
            []));

        Assert.True(sinais.MicrofoneAtivo);
        Assert.True(sinais.AudioSaidaAtivo);
        Assert.True(sinais.Ambiguo);
        Assert.Null(sinais.Plataforma);
    }

    [Fact]
    public void SnapshotPublicoNuncaDeveExporTituloPidOuDispositivo()
    {
        var propriedades = typeof(SinaisDeteccaoReuniao)
            .GetProperties()
            .Select(propriedade => propriedade.Name)
            .ToArray();

        Assert.DoesNotContain(propriedades, nome => nome.Contains("Titulo", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propriedades, nome => nome.Contains("Pid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propriedades, nome => nome.Contains("Dispositivo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FalhaParcialDeUmProbeDeveApenasReduzirConfianca()
    {
        var fonte = new WindowsSinaisReuniaoSource(
            new ProbeParcialmenteIndisponivel(),
            _classificador);

        var sinais = await fonte.ObterAsync(CancellationToken.None);

        Assert.Equal("native_teams", sinais.Plataforma!.Chave);
        Assert.True(sinais.MicrofoneAtivo);
        Assert.False(sinais.AudioSaidaAtivo);
        Assert.False(sinais.ColetaConfiavel);
    }

    [Fact]
    public async Task LeituraParcialDentroDoProbeDevePreservarDadosEMarcarDegradacao()
    {
        var fonte = new WindowsSinaisReuniaoSource(
            new ProbeComLeituraParcialInterna(),
            _classificador);

        var sinais = await fonte.ObterAsync(CancellationToken.None);

        Assert.Equal("native_teams", sinais.Plataforma!.Chave);
        Assert.True(sinais.MicrofoneAtivo);
        Assert.True(sinais.AudioSaidaAtivo);
        Assert.False(sinais.ColetaConfiavel);
    }

    private sealed class ProbeParcialmenteIndisponivel : IWindowsSinaisProbe
    {
        public IReadOnlyCollection<string> ListarFamiliasComCaptura() => ["ms-teams"];

        public IReadOnlyCollection<string> ListarFamiliasComRenderizacao() =>
            throw new InvalidOperationException("Serviço de áudio indisponível.");

        public IReadOnlyCollection<JanelaWindows> ListarJanelas() =>
            throw new InvalidOperationException("User32 indisponível.");
    }

    private sealed class ProbeComLeituraParcialInterna : IWindowsSinaisProbe
    {
        public IReadOnlyCollection<string> ListarFamiliasComCaptura() =>
            WindowsSinaisProbe.ConcluirLeitura(
                ["ms-teams"],
                leituraCompleta: false);

        public IReadOnlyCollection<string> ListarFamiliasComRenderizacao() => ["ms-teams"];

        public IReadOnlyCollection<JanelaWindows> ListarJanelas() => [];
    }
}

using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Observabilidade;
using Anamnesis.Infrastructure.Persistencia;
using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class CapturaInstantaneaE2ETests : IAsyncLifetime
{
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-deteccao-e2e-{Guid.NewGuid():N}");

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
    public async Task SinalFakeCliqueESqliteRealDevemPersistirGravacaoEJournalSeguro()
    {
        Directory.CreateDirectory(_diretorio);
        var banco = Path.Combine(_diretorio, "anamnesis.db");
        var relogio = new RelogioFixo();
        var reunioes = new SqliteReuniaoRepository(banco);
        var fila = new SqliteJobQueue(banco);
        var eventos = new SqliteEventoOperacionalRepository(banco);
        var journal = new JornalOperacional(eventos, relogio);
        var sessao = new DesktopRealSession(
            new SqliteReuniaoQuery(banco),
            new SqliteJobQuery(banco),
            new ControlarGravacaoHandler(
                reunioes,
                fila,
                new GravadorFake(),
                new WorkerFake(),
                new PreflightFake(),
                relogio,
                journal),
            new ArtefatoLauncherFake(),
            relogio,
            eventoQuery: eventos,
            jobMetricasQuery: new SqliteJobQuery(banco),
            journal: journal);
        await sessao.AtualizarAsync(CancellationToken.None);
        var detector = new DetectorReuniao(
            new FonteFake(),
            new PoliticaDeteccaoReuniao(PoliticaDeteccaoOptions.Padrao),
            relogio,
            journal);
        using var controller = new CapturaInstantaneaController(
            detector,
            sessao,
            new PromptFake());

        await controller.ProcessarAsync(CancellationToken.None);

        var persistida = Assert.Single(await new SqliteReuniaoQuery(banco).ListarAsync(
            new ReuniaoQueryFiltro(null, null, 10),
            CancellationToken.None));
        Assert.Equal(StatusReuniao.Gravando, persistida.Status);
        Assert.Equal("Reunião detectada • Google Meet", persistida.Titulo);
        var journalPersistido = await eventos.ListarAsync(
            new EventoOperacionalFiltro(Limite: 20),
            CancellationToken.None);
        Assert.Contains(
            journalPersistido,
            evento => evento.Codigo == CodigosEventoOperacional.DeteccaoDecidida);
        Assert.Contains(
            journalPersistido,
            evento => evento.Codigo == CodigosEventoOperacional.GravacaoIniciada &&
                evento.ReuniaoId == persistida.Id);
        Assert.DoesNotContain(
            journalPersistido,
            evento => evento.Mensagem.Contains("Google Meet", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FonteFake : ISinaisReuniaoSource
    {
        public Task<SinaisDeteccaoReuniao> ObterAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SinaisDeteccaoReuniao(
                true,
                true,
                new PlataformaLocal(
                    "browser_meet",
                    "Google Meet",
                    OrigemPlataformaLocal.Navegador),
                false,
                false));
    }

    private sealed class PromptFake : IDeteccaoPrompt
    {
        public Task<AcaoSugestaoDeteccao> SugerirInicioAsync(
            PlataformaLocal plataforma,
            CancellationToken cancellationToken) =>
            Task.FromResult(AcaoSugestaoDeteccao.Iniciar);

        public bool ConsumirCancelamentoInicio() => false;
        public void MostrarContagemInicio(PlataformaLocal plataforma, TimeSpan restante) { }
        public void FecharContagemInicio() { }
        public bool ConsumirCancelamentoEncerramento() => false;
        public void MostrarAvisoEncerramento(TimeSpan restante) { }
        public void FecharAvisoEncerramento() { }
        public void NotificarInicioAutomatico() { }
        public void MostrarFalhaSegura() { }
        public void Fechar() { }
        public void Dispose() { }
    }

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 5, 18, 0, 0, TimeSpan.Zero);
    }

    private sealed class GravadorFake : IGravador
    {
        public Task IniciarAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string> FinalizarAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Path.Combine(Path.GetTempPath(), "captura.mkv"));
        public Task<bool> EstaGravandoAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class PreflightFake : IObsPreflight
    {
        public Task PrepararAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class WorkerFake : IWorkerLauncher
    {
        public Task IniciarAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ArtefatoLauncherFake : IArtefatoLauncher
    {
        public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

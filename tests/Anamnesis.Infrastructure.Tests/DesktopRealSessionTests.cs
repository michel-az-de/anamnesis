using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DesktopRealSessionTests
{
    [Fact]
    public async Task DeveIniciarVazioEExecutarComandosReaisPeloCasoDeUso()
    {
        var relogio = new RelogioFixo(new DateTimeOffset(2026, 8, 5, 18, 0, 0, TimeSpan.Zero));
        var dados = new DadosEmMemoria();
        var gravador = new GravadorFake();
        var worker = new WorkerLauncherFake();
        var sessao = new DesktopRealSession(
            dados,
            dados,
            new ControlarGravacaoHandler(dados, dados, gravador, worker, new ObsPreflightFake(), relogio),
            new ArtefatoLauncherFake(),
            relogio);

        await sessao.AtualizarAsync(CancellationToken.None);

        Assert.False(sessao.ModoDemonstracao);
        Assert.Empty(sessao.Reunioes);

        await sessao.IniciarGravacaoAsync("Reunião real", CancellationToken.None);

        Assert.Equal(EtapaDesktopPoc.Gravando, sessao.Etapa);
        Assert.Equal(1, gravador.Inicios);
        Assert.Equal("Reunião real", Assert.Single(sessao.Reunioes).Titulo);

        await sessao.EncerrarGravacaoAsync(CancellationToken.None);

        Assert.Equal(EtapaDesktopPoc.Processando, sessao.Etapa);
        Assert.Equal(1, gravador.Encerramentos);
        Assert.Equal(1, worker.Chamadas);
        Assert.Equal(1, dados.JobsCriados);
        Assert.Equal("Aguardando processamento", Assert.Single(sessao.Reunioes).Status);
    }

    [Fact]
    public async Task DeveMapearDetalheJobECaminhosPersistidosSemRecalcular()
    {
        var reuniaoId = Guid.NewGuid();
        var detalhe = new ReuniaoDetalhe(
            reuniaoId,
            "Revisão real",
            new DateTimeOffset(2026, 8, 5, 14, 0, 0, TimeSpan.Zero),
            StatusReuniao.Arquivada,
            null,
            @"D:\gravacoes\revisao.mkv",
            new DateTimeOffset(2026, 8, 5, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 5, 14, 30, 0, TimeSpan.Zero),
            "Transcrição persistida.",
            "pt-BR",
            new DateTimeOffset(2026, 8, 5, 14, 32, 0, TimeSpan.Zero),
            "Resumo persistido.",
            ["Decisão persistida."],
            [new Tarefa("Tarefa persistida.", "Felipe", null)],
            new DateTimeOffset(2026, 8, 5, 14, 33, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 5, 14, 34, 0, TimeSpan.Zero),
            new ArtefatosReuniao(
                reuniaoId,
                @"D:\arquivo-antigo\revisao",
                @"D:\arquivo-antigo\revisao\ata.md",
                @"D:\arquivo-antigo\revisao\transcricao.md"));
        var query = new ReuniaoQueryFake(detalhe);
        var jobQuery = new JobQueryFake(new JobResumo(
            Guid.NewGuid(),
            reuniaoId,
            detalhe.CriadaEm,
            detalhe.CriadaEm.AddMinutes(31),
            detalhe.CriadaEm.AddMinutes(34),
            1,
            EstadoJobProcessamento.Concluido));
        var launcher = new ArtefatoLauncherFake();
        var sessao = new DesktopRealSession(
            query,
            jobQuery,
            CriarHandlerNulo(),
            launcher,
            TimeProvider.System);

        var representacao = await sessao.ObterDetalheAsync(reuniaoId, CancellationToken.None);

        Assert.NotNull(representacao);
        Assert.Equal("Resumo persistido.", representacao!.Resumo);
        Assert.Equal(["Transcrição persistida."], representacao.Transcricao);
        Assert.Equal(["Decisão persistida."], representacao.Decisoes);
        Assert.Equal(@"D:\arquivo-antigo\revisao\ata.md", representacao.CaminhoAta);
        Assert.Contains(representacao.PontosPrincipais, item => item.Contains("Concluido", StringComparison.Ordinal));

        await sessao.AbrirArquivoAsync(representacao.CaminhoAta!, CancellationToken.None);
        await sessao.MostrarNaPastaAsync(representacao.CaminhoAta!, CancellationToken.None);
        Assert.Equal(1, launcher.Aberturas);
        Assert.Equal(1, launcher.Exibicoes);
    }

    [Fact]
    public async Task DeveReconciliarGravacaoOrfaNaPrimeiraAtualizacao()
    {
        var dados = new DadosEmMemoria();
        var reuniao = new Reuniao(Guid.NewGuid(), "Órfã", DateTimeOffset.UtcNow);
        reuniao.IniciarGravacao(DateTimeOffset.UtcNow);
        await dados.SalvarAsync(reuniao, CancellationToken.None);
        var gravador = new GravadorFake(estaGravando: false);
        var sessao = new DesktopRealSession(
            dados,
            dados,
            new ControlarGravacaoHandler(
                dados,
                dados,
                gravador,
                new WorkerLauncherFake(),
                new ObsPreflightFake(),
                TimeProvider.System),
            new ArtefatoLauncherFake(),
            TimeProvider.System);

        await sessao.AtualizarAsync(CancellationToken.None);

        Assert.Equal("Falha", Assert.Single(sessao.Reunioes).Status);
        Assert.Equal(1, gravador.ConsultasEstado);
    }

    [Fact]
    public async Task DeveReconciliarGravacaoOrfaEmAtualizacaoPosterior()
    {
        var dados = new DadosEmMemoria();
        var gravador = new GravadorFake(estaGravando: false);
        var sessao = new DesktopRealSession(
            dados,
            dados,
            new ControlarGravacaoHandler(
                dados,
                dados,
                gravador,
                new WorkerLauncherFake(),
                new ObsPreflightFake(),
                TimeProvider.System),
            new ArtefatoLauncherFake(),
            TimeProvider.System);

        await sessao.AtualizarAsync(CancellationToken.None);
        Assert.Empty(sessao.Reunioes);

        // A gravação órfã só aparece depois da primeira atualização: Tray encerrado à força
        // durante uma gravação, ou alteração externa no banco.
        var reuniao = new Reuniao(Guid.NewGuid(), "Órfã tardia", DateTimeOffset.UtcNow);
        reuniao.IniciarGravacao(DateTimeOffset.UtcNow);
        await dados.SalvarAsync(reuniao, CancellationToken.None);

        await sessao.AtualizarAsync(CancellationToken.None);

        Assert.Equal("Falha", Assert.Single(sessao.Reunioes).Status);
    }

    [Fact]
    public async Task DeveEnviarUmUnicoStopQuandoDoisEncerramentosConcorrem()
    {
        var dados = new DadosEmMemoria();
        var gravador = new GravadorBloqueadoFake();
        var sessao = new DesktopRealSession(
            dados,
            dados,
            new ControlarGravacaoHandler(
                dados,
                dados,
                gravador,
                new WorkerLauncherFake(),
                new ObsPreflightFake(),
                TimeProvider.System),
            new ArtefatoLauncherFake(),
            TimeProvider.System);
        await sessao.IniciarGravacaoAsync("Concorrente", CancellationToken.None);

        var primeiro = sessao.EncerrarGravacaoAsync(CancellationToken.None);
        await gravador.AguardarFinalizacaoAsync();
        var segundo = sessao.EncerrarGravacaoAsync(CancellationToken.None);
        gravador.LiberarFinalizacao();

        await primeiro;
        await Assert.ThrowsAsync<InvalidOperationException>(() => segundo);
        Assert.Equal(1, gravador.Encerramentos);
    }

    [Fact]
    public async Task DeveRecusarSegundoInicioConcorrenteSemNovaChamadaAoObs()
    {
        var dados = new DadosEmMemoria();
        var gravador = new GravadorInicioBloqueadoFake();
        var sessao = new DesktopRealSession(
            dados,
            dados,
            new ControlarGravacaoHandler(
                dados,
                dados,
                gravador,
                new WorkerLauncherFake(),
                new ObsPreflightFake(),
                TimeProvider.System),
            new ArtefatoLauncherFake(),
            TimeProvider.System);

        var primeiro = sessao.IniciarGravacaoAsync("Primeiro início", CancellationToken.None);
        await gravador.AguardarInicioAsync();
        var segundo = sessao.IniciarGravacaoAsync("Segundo início", CancellationToken.None);
        gravador.LiberarInicio();

        await primeiro;
        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() => segundo);
        Assert.Equal("Já existe uma gravação em andamento.", excecao.Message);
        Assert.Equal(1, gravador.Inicios);
    }

    private static ControlarGravacaoHandler CriarHandlerNulo()
    {
        var dados = new DadosEmMemoria();
        return new ControlarGravacaoHandler(
            dados,
            dados,
            new GravadorFake(),
            new WorkerLauncherFake(),
            new ObsPreflightFake(),
            TimeProvider.System);
    }

    private sealed class DadosEmMemoria : IReuniaoRepository, IReuniaoQuery, IJobQueue, IJobQuery
    {
        private readonly Dictionary<Guid, Reuniao> _reunioes = [];
        private readonly List<JobResumo> _jobs = [];

        public int JobsCriados => _jobs.Count;

        public Task SalvarAsync(Reuniao reuniao, CancellationToken cancellationToken)
        {
            _reunioes[reuniao.Id] = reuniao;
            return Task.CompletedTask;
        }

        public Task<Reuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult(_reunioes.GetValueOrDefault(reuniaoId));

        public Task<IReadOnlyList<ReuniaoResumo>> ListarAsync(
            ReuniaoQueryFiltro filtro,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReuniaoResumo>>(_reunioes.Values
                .OrderByDescending(reuniao => reuniao.CriadaEm)
                .Select(reuniao => new ReuniaoResumo(
                    reuniao.Id,
                    reuniao.Titulo,
                    reuniao.CriadaEm,
                    reuniao.Status,
                    reuniao.Gravacao?.IniciadaEm,
                    reuniao.Gravacao?.FinalizadaEm,
                    reuniao.MotivoFalha))
                .ToArray());

        public Task<ReuniaoDetalhe?> ObterDetalheAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<ReuniaoDetalhe?>(null);

        public Task<JobProcessamento> EnfileirarAsync(
            Guid reuniaoId,
            DateTimeOffset criadoEm,
            CancellationToken cancellationToken)
        {
            var job = new JobProcessamento(Guid.NewGuid(), reuniaoId, criadoEm, null, 0);
            _jobs.Add(new JobResumo(job.Id, reuniaoId, criadoEm, null, null, 0, EstadoJobProcessamento.Pendente));
            return Task.FromResult(job);
        }

        public Task<JobProcessamento?> ReservarProximoAsync(DateTimeOffset reservadoEm, CancellationToken cancellationToken) =>
            Task.FromResult<JobProcessamento?>(null);

        public Task LiberarAsync(Guid jobId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LiberarReservasAtivasAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ConcluirAsync(Guid jobId, DateTimeOffset concluidoEm, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<JobResumo?> ObterMaisRecenteAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult(_jobs.LastOrDefault(job => job.ReuniaoId == reuniaoId));
    }

    private sealed class ReuniaoQueryFake(ReuniaoDetalhe detalhe) : IReuniaoQuery
    {
        public Task<IReadOnlyList<ReuniaoResumo>> ListarAsync(ReuniaoQueryFiltro filtro, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReuniaoResumo>>([]);

        public Task<ReuniaoDetalhe?> ObterDetalheAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<ReuniaoDetalhe?>(reuniaoId == detalhe.Id ? detalhe : null);
    }

    private sealed class JobQueryFake(JobResumo job) : IJobQuery
    {
        public Task<JobResumo?> ObterMaisRecenteAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<JobResumo?>(reuniaoId == job.ReuniaoId ? job : null);
    }

    private sealed class GravadorFake(bool estaGravando = true) : IGravador
    {
        public int Inicios { get; private set; }
        public int Encerramentos { get; private set; }
        public int ConsultasEstado { get; private set; }

        public Task IniciarAsync(CancellationToken cancellationToken)
        {
            Inicios++;
            return Task.CompletedTask;
        }

        public Task<string> FinalizarAsync(CancellationToken cancellationToken)
        {
            Encerramentos++;
            return Task.FromResult(@"C:\gravacoes\real.mkv");
        }

        public Task<bool> EstaGravandoAsync(CancellationToken cancellationToken)
        {
            ConsultasEstado++;
            return Task.FromResult(estaGravando);
        }
    }

    private sealed class GravadorBloqueadoFake : IGravador
    {
        private readonly TaskCompletionSource _finalizacaoIniciada = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _liberarFinalizacao = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Encerramentos { get; private set; }

        public Task IniciarAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<string> FinalizarAsync(CancellationToken cancellationToken)
        {
            Encerramentos++;
            _finalizacaoIniciada.TrySetResult();
            await _liberarFinalizacao.Task.WaitAsync(cancellationToken);
            return @"C:\gravacoes\real.mkv";
        }

        public Task<bool> EstaGravandoAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task AguardarFinalizacaoAsync() => _finalizacaoIniciada.Task;

        public void LiberarFinalizacao() => _liberarFinalizacao.TrySetResult();
    }

    private sealed class GravadorInicioBloqueadoFake : IGravador
    {
        private readonly TaskCompletionSource _inicioAcionado = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _liberarInicio = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Inicios { get; private set; }

        public async Task IniciarAsync(CancellationToken cancellationToken)
        {
            Inicios++;
            _inicioAcionado.TrySetResult();
            await _liberarInicio.Task.WaitAsync(cancellationToken);
        }

        public Task<string> FinalizarAsync(CancellationToken cancellationToken) =>
            Task.FromResult(@"C:\gravacoes\real.mkv");

        public Task<bool> EstaGravandoAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task AguardarInicioAsync() => _inicioAcionado.Task;

        public void LiberarInicio() => _liberarInicio.TrySetResult();
    }

    private sealed class WorkerLauncherFake : IWorkerLauncher
    {
        public int Chamadas { get; private set; }

        public Task IniciarAsync(CancellationToken cancellationToken)
        {
            Chamadas++;
            return Task.CompletedTask;
        }
    }

    private sealed class ObsPreflightFake : IObsPreflight
    {
        public Task PrepararAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ArtefatoLauncherFake : IArtefatoLauncher
    {
        public int Aberturas { get; private set; }
        public int Exibicoes { get; private set; }

        public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken)
        {
            Aberturas++;
            return Task.CompletedTask;
        }

        public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken)
        {
            Exibicoes++;
            return Task.CompletedTask;
        }
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}

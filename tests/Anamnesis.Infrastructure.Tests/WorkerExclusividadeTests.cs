using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Observabilidade;
using Anamnesis.Infrastructure.Persistencia;
using Anamnesis.Infrastructure.Processos;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WorkerExclusividadeTests : IAsyncLifetime
{
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-worker-exclusividade-{Guid.NewGuid():N}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_diretorio);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_diretorio))
        {
            Directory.Delete(_diretorio, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task DeveEncerrarSemTocarFilaEPersistirDiagnosticoQuandoLimiteExpira()
    {
        var caminhoBanco = Path.Combine(_diretorio, "anamnesis.db");
        var caminhoConfiguracao = Path.Combine(_diretorio, "config.json");
        var caminhoGravacao = Path.Combine(_diretorio, "gravacao.mkv");
        await File.WriteAllTextAsync(caminhoGravacao, "gravacao preservada");
        await new ArquivoConfiguracao(caminhoConfiguracao).SalvarAsync(
            new ConfiguracaoAnamnesis
            {
                CaminhoBanco = caminhoBanco,
                DiretorioArquivo = Path.Combine(_diretorio, "arquivo")
            },
            CancellationToken.None);

        var agora = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
        var reuniao = new Reuniao(Guid.NewGuid(), "Contencao do Worker", agora);
        reuniao.IniciarGravacao(agora);
        reuniao.FinalizarGravacao(caminhoGravacao, agora.AddMinutes(1));
        var reuniaoRepository = new SqliteReuniaoRepository(caminhoBanco);
        await reuniaoRepository.SalvarAsync(reuniao, CancellationToken.None);
        await new SqliteJobQueue(caminhoBanco).EnfileirarAsync(
            reuniao.Id,
            agora.AddMinutes(1),
            CancellationToken.None);
        var mensagens = new List<string>();

        var codigoSaida = Anamnesis.Worker.Program.Executar(
            [],
            caminhoConfiguracao,
            _ => null,
            mensagens.Add);

        Assert.Equal(0, codigoSaida);
        Assert.Contains(mensagens, mensagem =>
            mensagem.Contains("fila permanece intacta", StringComparison.OrdinalIgnoreCase));
        var job = await new SqliteJobQuery(caminhoBanco).ObterMaisRecenteAsync(
            reuniao.Id,
            CancellationToken.None);
        Assert.NotNull(job);
        Assert.Equal(EstadoJobProcessamento.Pendente, job.Estado);
        Assert.Equal(0, job.Tentativas);
        Assert.Null(job.ReservadoEm);
        Assert.Null(job.ConcluidoEm);
        Assert.Equal(StatusReuniao.AguardandoProcessamento, (await reuniaoRepository.ObterAsync(
            reuniao.Id,
            CancellationToken.None))!.Status);

        var eventos = await new SqliteEventoOperacionalRepository(caminhoBanco).ListarAsync(
            new EventoOperacionalFiltro(
                Codigo: CodigosEventoOperacional.WorkerExclusividadeExpirada),
            CancellationToken.None);
        var evento = Assert.Single(eventos);
        Assert.Equal(NivelEventoOperacional.Info, evento.Nivel);
        Assert.Equal("Worker", evento.Componente);
        Assert.Equal("aguardar_exclusividade", evento.Metadados.Operacao);
        Assert.Equal("limite", evento.Metadados.Resultado);
        Assert.Null(evento.ReuniaoId);
        Assert.Null(evento.JobId);
    }
}

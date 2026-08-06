using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Xunit;

namespace Anamnesis.Application.Tests;

public sealed class JornalOperacionalTests
{
    [Fact]
    public void DeveExporCatalogoMinimoUnicoEEstavel()
    {
        string[] esperados =
        [
            "gravacao.iniciada",
            "gravacao.finalizada",
            "job.enfileirado",
            "job.reservado",
            "transcricao.iniciada",
            "transcricao.concluida",
            "ata.gerada",
            "reuniao.arquivada",
            "job.concluido",
            "retencao.avaliada",
            "retencao.aplicada",
            "operacao.falhou",
            "deteccao.decidida"
        ];

        Assert.Equal(esperados, CodigosEventoOperacional.Todos);
        Assert.Equal(esperados.Length, CodigosEventoOperacional.Todos.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task DeveRedigirMensagemAntesDeEnviarAoSink()
    {
        var sink = new SinkCapturador();
        var relogio = new RelogioFixo(new DateTimeOffset(2026, 8, 5, 22, 0, 0, TimeSpan.Zero));
        var journal = new JornalOperacional(sink, relogio);
        var reuniaoId = Guid.NewGuid();

        await journal.RegistrarAsync(
            NivelEventoOperacional.Erro,
            CodigosEventoOperacional.OperacaoFalhou,
            "Whisper",
            "senha=segredo token=abc123 em C:\\Users\\felip\\reuniao.txt\nsegunda linha",
            reuniaoId,
            null,
            new MetadadosEventoOperacional(Operacao: "transcrever"),
            CancellationToken.None);

        var evento = Assert.Single(sink.Eventos);
        Assert.Equal(relogio.GetUtcNow(), evento.CriadoEm);
        Assert.Equal(reuniaoId, evento.ReuniaoId);
        Assert.DoesNotContain("segredo", evento.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc123", evento.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("felip", evento.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\n', evento.Mensagem);
        Assert.Contains("[REMOVIDO]", evento.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FalhaDoSinkNaoDeveEscaparParaOCasoDeUso()
    {
        var journal = new JornalOperacional(new SinkComFalha(), TimeProvider.System);

        var falha = await Record.ExceptionAsync(() => journal.RegistrarAsync(
            NivelEventoOperacional.Info,
            CodigosEventoOperacional.GravacaoIniciada,
            "OBS",
            "Gravação local iniciada.",
            Guid.NewGuid(),
            null,
            null,
            CancellationToken.None));

        Assert.Null(falha);
    }

    [Fact]
    public async Task DeveLimitarRetencaoEntreUmENoventaDias()
    {
        var sink = new SinkCapturador();
        var agora = new DateTimeOffset(2026, 8, 5, 22, 0, 0, TimeSpan.Zero);
        var journal = new JornalOperacional(sink, new RelogioFixo(agora));

        await journal.RemoverExpiradosAsync(0, CancellationToken.None);
        Assert.Equal(agora.AddDays(-1), sink.LimiteRemocao);

        await journal.RemoverExpiradosAsync(200, CancellationToken.None);
        Assert.Equal(agora.AddDays(-90), sink.LimiteRemocao);
    }

    [Fact]
    public async Task FalhaNaoDevePersistirMensagemLivreDaExcecao()
    {
        var sink = new SinkCapturador();
        var journal = new JornalOperacional(sink, TimeProvider.System);

        await journal.RegistrarFalhaAsync(
            "CLI",
            "gerar_ata",
            new InvalidOperationException(
                "Transcrição confidencial; --token segredo; C:\\Users\\felip\\prompt.txt"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        var evento = Assert.Single(sink.Eventos);
        Assert.Equal("A operação local falhou.", evento.Mensagem);
        Assert.Equal(nameof(InvalidOperationException), evento.Metadados.MotivoCodigo);
        Assert.DoesNotContain("Transcrição", evento.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", evento.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SinkCapturador : IEventoOperacionalSink
    {
        public List<EventoOperacional> Eventos { get; } = [];
        public DateTimeOffset? LimiteRemocao { get; private set; }

        public Task RegistrarAsync(EventoOperacional evento, CancellationToken cancellationToken)
        {
            Eventos.Add(evento);
            return Task.CompletedTask;
        }

        public Task RemoverAnterioresAsync(DateTimeOffset limiteUtc, CancellationToken cancellationToken)
        {
            LimiteRemocao = limiteUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class SinkComFalha : IEventoOperacionalSink
    {
        public Task RegistrarAsync(EventoOperacional evento, CancellationToken cancellationToken) =>
            throw new IOException("Journal indisponível.");

        public Task RemoverAnterioresAsync(DateTimeOffset limiteUtc, CancellationToken cancellationToken) =>
            throw new IOException("Journal indisponível.");
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}

using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Observabilidade;

public sealed class JornalOperacional(
    IEventoOperacionalSink sink,
    TimeProvider relogio)
{
    public static JornalOperacional Nulo { get; } = new(new SinkNulo(), TimeProvider.System);

    public async Task RegistrarAsync(
        NivelEventoOperacional nivel,
        string codigo,
        string componente,
        string mensagem,
        Guid? reuniaoId,
        Guid? jobId,
        MetadadosEventoOperacional? metadados,
        CancellationToken cancellationToken)
    {
        try
        {
            var evento = new EventoOperacional(
                Guid.NewGuid(),
                relogio.GetUtcNow().ToUniversalTime(),
                nivel,
                codigo,
                componente,
                RedatorMensagemOperacional.Redigir(mensagem),
                reuniaoId,
                jobId,
                Normalizar(metadados));
            await sink.RegistrarAsync(evento, cancellationToken);
        }
        catch (Exception)
        {
            // Observabilidade e best-effort e nunca participa do resultado do caso de uso.
        }
    }

    public Task RegistrarFalhaAsync(
        string componente,
        string operacao,
        Exception exception,
        Guid? reuniaoId,
        Guid? jobId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return RegistrarAsync(
            NivelEventoOperacional.Erro,
            CodigosEventoOperacional.OperacaoFalhou,
            componente,
            "A operação local falhou.",
            reuniaoId,
            jobId,
            new MetadadosEventoOperacional(
                Operacao: operacao,
                Resultado: "falha",
                MotivoCodigo: exception.GetType().Name),
            cancellationToken);
    }

    public async Task RemoverExpiradosAsync(int retencaoDias, CancellationToken cancellationToken)
    {
        try
        {
            var dias = Math.Clamp(retencaoDias, 1, 90);
            var limite = relogio.GetUtcNow().ToUniversalTime().AddDays(-dias);
            await sink.RemoverAnterioresAsync(limite, cancellationToken);
        }
        catch (Exception)
        {
            // Limpeza do journal tambem nao pode afetar a inicializacao do produto.
        }
    }

    private static MetadadosEventoOperacional Normalizar(MetadadosEventoOperacional? metadados) =>
        metadados is null
            ? new MetadadosEventoOperacional()
            : metadados with
            {
                Operacao = RedigirCodigo(metadados.Operacao),
                Tentativa = metadados.Tentativa is < 0 ? 0 : metadados.Tentativa,
                Resultado = RedigirCodigo(metadados.Resultado),
                MotivoCodigo = RedigirCodigo(metadados.MotivoCodigo),
                DuracaoMs = metadados.DuracaoMs is < 0 ? 0 : metadados.DuracaoMs
            };

    private static string? RedigirCodigo(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = valor.Trim();
        return normalizado.Length <= 64 ? normalizado : normalizado[..64];
    }

    private sealed class SinkNulo : IEventoOperacionalSink
    {
        public Task RegistrarAsync(EventoOperacional evento, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoverAnterioresAsync(DateTimeOffset limiteUtc, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

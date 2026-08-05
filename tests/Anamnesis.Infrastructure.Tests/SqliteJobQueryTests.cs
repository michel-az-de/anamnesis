using Anamnesis.Application.Modelos;
using Anamnesis.Infrastructure.Fila;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class SqliteJobQueryTests : IAsyncLifetime
{
    private readonly string _caminhoBanco = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-job-query-{Guid.NewGuid():N}.db");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (File.Exists(_caminhoBanco))
        {
            File.Delete(_caminhoBanco);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task DeveDerivarEstadoPendenteEmProcessamentoEConcluido()
    {
        var reuniaoPendente = Guid.NewGuid();
        var reuniaoProcessando = Guid.NewGuid();
        var reuniaoConcluida = Guid.NewGuid();
        var criadaEm = new DateTimeOffset(2026, 8, 5, 14, 0, 0, TimeSpan.Zero);
        var fila = new SqliteJobQueue(_caminhoBanco);
        await fila.EnfileirarAsync(reuniaoProcessando, criadaEm, CancellationToken.None);
        var reservadoProcessando = await fila.ReservarProximoAsync(criadaEm.AddMinutes(1), CancellationToken.None);
        Assert.Equal(reuniaoProcessando, reservadoProcessando!.ReuniaoId);
        await fila.EnfileirarAsync(reuniaoConcluida, criadaEm.AddMinutes(2), CancellationToken.None);
        var reservadoConcluido = await fila.ReservarProximoAsync(criadaEm.AddMinutes(3), CancellationToken.None);
        Assert.Equal(reuniaoConcluida, reservadoConcluido!.ReuniaoId);
        await fila.ConcluirAsync(reservadoConcluido.Id, criadaEm.AddMinutes(4), CancellationToken.None);
        await fila.EnfileirarAsync(reuniaoPendente, criadaEm.AddMinutes(5), CancellationToken.None);
        var query = new SqliteJobQuery(_caminhoBanco);

        var pendente = await query.ObterMaisRecenteAsync(reuniaoPendente, CancellationToken.None);
        var processando = await query.ObterMaisRecenteAsync(reuniaoProcessando, CancellationToken.None);
        var concluido = await query.ObterMaisRecenteAsync(reuniaoConcluida, CancellationToken.None);

        Assert.Equal(EstadoJobProcessamento.Pendente, pendente!.Estado);
        Assert.Equal(EstadoJobProcessamento.EmProcessamento, processando!.Estado);
        Assert.Equal(EstadoJobProcessamento.Concluido, concluido!.Estado);
        Assert.NotNull(concluido.ConcluidoEm);
    }

    [Fact]
    public async Task DeveRetornarJobMaisRecentePorCriacao()
    {
        var reuniaoId = Guid.NewGuid();
        var criadaEm = new DateTimeOffset(2026, 8, 5, 14, 0, 0, TimeSpan.Zero);
        var fila = new SqliteJobQueue(_caminhoBanco);
        var primeiro = await fila.EnfileirarAsync(reuniaoId, criadaEm, CancellationToken.None);
        var reservado = await fila.ReservarProximoAsync(criadaEm.AddMinutes(1), CancellationToken.None);
        await fila.ConcluirAsync(reservado!.Id, criadaEm.AddMinutes(2), CancellationToken.None);
        var segundo = await fila.EnfileirarAsync(reuniaoId, criadaEm.AddMinutes(3), CancellationToken.None);
        var query = new SqliteJobQuery(_caminhoBanco);

        var maisRecente = await query.ObterMaisRecenteAsync(reuniaoId, CancellationToken.None);

        Assert.NotEqual(primeiro.Id, segundo.Id);
        Assert.Equal(segundo.Id, maisRecente!.Id);
        Assert.Equal(EstadoJobProcessamento.Pendente, maisRecente.Estado);
    }

    [Fact]
    public async Task DeveContarSomenteJobsAindaNaoConcluidos()
    {
        var fila = new SqliteJobQueue(_caminhoBanco);
        var agora = new DateTimeOffset(2026, 8, 5, 14, 0, 0, TimeSpan.Zero);
        await fila.EnfileirarAsync(Guid.NewGuid(), agora, CancellationToken.None);
        await fila.EnfileirarAsync(Guid.NewGuid(), agora.AddMinutes(1), CancellationToken.None);
        var reservado = await fila.ReservarProximoAsync(agora.AddMinutes(2), CancellationToken.None);
        await fila.ConcluirAsync(reservado!.Id, agora.AddMinutes(3), CancellationToken.None);
        var query = new SqliteJobQuery(_caminhoBanco);

        var pendentes = await query.ContarPendentesAsync(CancellationToken.None);

        Assert.Equal(1, pendentes);
    }
}

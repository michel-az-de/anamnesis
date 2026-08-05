using Anamnesis.Application.Modelos;
using Anamnesis.Infrastructure.Fila;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class SqliteJobQueueTests : IAsyncLifetime
{
    private readonly string _caminhoBanco = Path.Combine(Path.GetTempPath(), $"anamnesis-{Guid.NewGuid():N}.db");

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
    public async Task DeveUsarEngineComSuporteARetorno()
    {
        await using var conexao = new SqliteConnection($"Data Source={_caminhoBanco};Pooling=False");
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT sqlite_version();";
        var versao = new Version((string)(await comando.ExecuteScalarAsync())!);

        // A reserva atômica usa RETURNING, que exige 3.35 ou superior. Com a engine do sistema
        // isto dependia do build do Windows; com a engine embarcada é uma garantia do pacote.
        Assert.True(versao >= new Version(3, 35), $"Engine SQLite {versao} não suporta RETURNING.");
    }

    [Fact]
    public async Task DeveReservarUmJobUmaUnicaVezEnquantoEstiverReservado()
    {
        var fila = new SqliteJobQueue(_caminhoBanco);
        var criadoEm = new DateTimeOffset(2026, 8, 4, 14, 0, 0, TimeSpan.Zero);
        await fila.EnfileirarAsync(Guid.NewGuid(), criadoEm, CancellationToken.None);

        var primeiro = await fila.ReservarProximoAsync(criadoEm.AddMinutes(1), CancellationToken.None);
        var segundo = await fila.ReservarProximoAsync(criadoEm.AddMinutes(2), CancellationToken.None);

        Assert.NotNull(primeiro);
        Assert.Null(segundo);
        Assert.Equal(1, primeiro!.Tentativas);
    }

    [Fact]
    public async Task DevePermitirReservarNovamenteDepoisDeLiberar()
    {
        var fila = new SqliteJobQueue(_caminhoBanco);
        var criadoEm = new DateTimeOffset(2026, 8, 4, 14, 0, 0, TimeSpan.Zero);
        await fila.EnfileirarAsync(Guid.NewGuid(), criadoEm, CancellationToken.None);

        var reservado = await fila.ReservarProximoAsync(criadoEm.AddMinutes(1), CancellationToken.None);
        await fila.LiberarAsync(reservado!.Id, CancellationToken.None);
        var reservadoNovamente = await fila.ReservarProximoAsync(criadoEm.AddMinutes(2), CancellationToken.None);

        Assert.NotNull(reservadoNovamente);
        Assert.Equal(reservado.Id, reservadoNovamente!.Id);
        Assert.Equal(2, reservadoNovamente.Tentativas);
    }

    [Fact]
    public async Task DeveManterUmUnicoJobAtivoPorReuniao()
    {
        var fila = new SqliteJobQueue(_caminhoBanco);
        var reuniaoId = Guid.NewGuid();
        var criadoEm = new DateTimeOffset(2026, 8, 4, 14, 0, 0, TimeSpan.Zero);

        var primeiro = await fila.EnfileirarAsync(reuniaoId, criadoEm, CancellationToken.None);
        var segundo = await fila.EnfileirarAsync(reuniaoId, criadoEm.AddMinutes(1), CancellationToken.None);

        Assert.Equal(primeiro.Id, segundo.Id);
    }

    [Fact]
    public async Task NaoDeveReservarJobConcluido()
    {
        var fila = new SqliteJobQueue(_caminhoBanco);
        var criadoEm = new DateTimeOffset(2026, 8, 4, 14, 0, 0, TimeSpan.Zero);
        await fila.EnfileirarAsync(Guid.NewGuid(), criadoEm, CancellationToken.None);

        var reservado = await fila.ReservarProximoAsync(criadoEm.AddMinutes(1), CancellationToken.None);
        await fila.ConcluirAsync(reservado!.Id, criadoEm.AddMinutes(2), CancellationToken.None);

        var proximo = await fila.ReservarProximoAsync(criadoEm.AddMinutes(3), CancellationToken.None);

        Assert.Null(proximo);
    }

    [Fact]
    public async Task DeveLiberarReservasAtivasParaRetomada()
    {
        var fila = new SqliteJobQueue(_caminhoBanco);
        var criadoEm = new DateTimeOffset(2026, 8, 4, 14, 0, 0, TimeSpan.Zero);
        await fila.EnfileirarAsync(Guid.NewGuid(), criadoEm, CancellationToken.None);
        await fila.ReservarProximoAsync(criadoEm.AddMinutes(1), CancellationToken.None);

        await fila.LiberarReservasAtivasAsync(CancellationToken.None);
        var retomado = await fila.ReservarProximoAsync(criadoEm.AddMinutes(2), CancellationToken.None);

        Assert.NotNull(retomado);
        Assert.Equal(2, retomado!.Tentativas);
    }

    [Fact]
    public async Task DeveOrdenarAFilaPeloInstanteUtcENaoPeloTextoDaData()
    {
        var fila = new SqliteJobQueue(_caminhoBanco);
        var primeiraReuniao = Guid.NewGuid();
        var segundaReuniao = Guid.NewGuid();

        // 08:00 em -03:00 é 11:00 UTC, anterior a 10:00 UTC? Não: é posterior.
        // Sem normalização, "08" ordena antes de "10" e a fila sairia invertida.
        await fila.EnfileirarAsync(
            segundaReuniao,
            new DateTimeOffset(2026, 8, 4, 8, 0, 0, TimeSpan.FromHours(-3)),
            CancellationToken.None);
        await fila.EnfileirarAsync(
            primeiraReuniao,
            new DateTimeOffset(2026, 8, 4, 10, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        var reservado = await fila.ReservarProximoAsync(
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.NotNull(reservado);
        Assert.Equal(primeiraReuniao, reservado!.ReuniaoId);
    }

    [Fact]
    public async Task DeveEnfileirarUmaUnicaVezSobConcorrencia()
    {
        var fila = new SqliteJobQueue(_caminhoBanco);
        var reuniaoId = Guid.NewGuid();
        var criadoEm = new DateTimeOffset(2026, 8, 4, 14, 0, 0, TimeSpan.Zero);
        var sinal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<JobProcessamento> EnfileirarAsync(int indice)
        {
            await sinal.Task;
            return await fila.EnfileirarAsync(
                reuniaoId,
                criadoEm.AddSeconds(indice),
                CancellationToken.None);
        }

        var tentativas = Enumerable.Range(0, 4).Select(EnfileirarAsync).ToArray();
        sinal.SetResult();
        var jobs = await Task.WhenAll(tentativas);

        Assert.Single(jobs.Select(job => job.Id).Distinct());
    }
}

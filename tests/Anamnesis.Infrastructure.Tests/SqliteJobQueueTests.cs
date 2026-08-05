using Anamnesis.Infrastructure.Fila;
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
}

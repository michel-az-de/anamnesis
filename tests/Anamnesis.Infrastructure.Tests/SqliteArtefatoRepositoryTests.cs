using System.Globalization;
using Anamnesis.Application.Modelos;
using Anamnesis.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class SqliteArtefatoRepositoryTests : IAsyncLifetime
{
    private readonly string _caminhoBanco = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-artefato-{Guid.NewGuid():N}.db");

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
    public async Task DevePersistirManifestoEntreInstanciasSemRecalcularRaiz()
    {
        var reuniaoId = Guid.NewGuid();
        var esperado = new ArtefatosReuniao(
            reuniaoId,
            @"D:\arquivo-antigo\2026\08\reuniao",
            @"D:\arquivo-antigo\2026\08\reuniao\ata.md",
            @"D:\arquivo-antigo\2026\08\reuniao\transcricao.md");
        await new SqliteArtefatoRepository(_caminhoBanco)
            .SalvarAsync(esperado, CancellationToken.None);

        var recuperado = await new SqliteArtefatoRepository(_caminhoBanco)
            .ObterAsync(reuniaoId, CancellationToken.None);

        Assert.Equal(esperado, recuperado);
    }

    [Fact]
    public async Task DeveUsarORelogioInjetadoParaOInstanteDeArquivamento()
    {
        var reuniaoId = Guid.NewGuid();
        var arquivadoEm = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        await new SqliteArtefatoRepository(_caminhoBanco, new RelogioFixo(arquivadoEm)).SalvarAsync(
            new ArtefatosReuniao(reuniaoId, @"D:\a", @"D:\a\ata.md", @"D:\a\transcricao.md"),
            CancellationToken.None);

        await using var conexao = new SqliteConnection($"Data Source={_caminhoBanco};Pooling=False");
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT arquivado_em FROM reuniao_artefatos WHERE reuniao_id = $id;";
        comando.Parameters.AddWithValue("$id", reuniaoId.ToString("N"));

        Assert.Equal(
            arquivadoEm.ToString("O", CultureInfo.InvariantCulture),
            (string)(await comando.ExecuteScalarAsync())!);
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}


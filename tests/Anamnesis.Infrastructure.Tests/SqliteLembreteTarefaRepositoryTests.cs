using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Persistencia;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class SqliteLembreteTarefaRepositoryTests
{
    [Fact]
    public async Task DevePersistirLembreteEImpedirNovoDisparoAposReinicio()
    {
        var diretorio = Path.Combine(Path.GetTempPath(), $"anamnesis-lembrete-{Guid.NewGuid():N}");
        Directory.CreateDirectory(diretorio);
        var banco = Path.Combine(diretorio, "anamnesis.db");
        try
        {
            var agora = new DateTimeOffset(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
            var lembrete = LembreteTarefa.Criar(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Revisar contrato.",
                agora,
                agora.AddDays(-1));
            var primeiroRepository = new SqliteLembreteTarefaRepository(banco);
            await primeiroRepository.SalvarAsync(lembrete, CancellationToken.None);

            var pendente = Assert.Single(await primeiroRepository.ListarPendentesAteAsync(
                agora,
                CancellationToken.None));
            pendente.MarcarNotificado(agora);
            await primeiroRepository.SalvarAsync(pendente, CancellationToken.None);

            var repositoryAposReinicio = new SqliteLembreteTarefaRepository(banco);
            Assert.Empty(await repositoryAposReinicio.ListarPendentesAteAsync(
                agora.AddDays(1),
                CancellationToken.None));
            Assert.Equal(StatusLembreteTarefa.Notificado, pendente.Status);
        }
        finally
        {
            Directory.Delete(diretorio, recursive: true);
        }
    }
}

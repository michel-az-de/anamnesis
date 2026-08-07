using Anamnesis.Application.Contracts;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Xunit;

namespace Anamnesis.Application.Tests;

public sealed class LembretesTarefaTests
{
    [Fact]
    public async Task DeveCriarLembreteFuturoEConservarTextoDaTarefa()
    {
        var agora = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        var repository = new LembreteRepositoryFake();
        var handler = new CriarLembreteTarefaHandler(repository, new RelogioFixo(agora));
        var reuniaoId = Guid.NewGuid();

        var lembrete = await handler.ExecutarAsync(
            reuniaoId,
            "Enviar proposta revisada.",
            agora.AddDays(1),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, lembrete.Id);
        Assert.Equal(reuniaoId, lembrete.ReuniaoId);
        Assert.Equal("Enviar proposta revisada.", lembrete.DescricaoTarefa);
        Assert.Equal(StatusLembreteTarefa.Pendente, lembrete.Status);
        Assert.Same(lembrete, Assert.Single(repository.Itens));
    }

    [Fact]
    public async Task DeveNotificarSomenteUmaVezMesmoEmConsultasRepetidas()
    {
        var agora = new DateTimeOffset(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
        var lembrete = LembreteTarefa.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Confirmar entrega.",
            agora.AddMinutes(-1),
            agora.AddDays(-1));
        var repository = new LembreteRepositoryFake(lembrete);
        var notificador = new NotificadorFake();
        var handler = new DispararLembretesTarefaHandler(
            repository,
            notificador,
            new RelogioFixo(agora));

        var primeiroDisparo = await handler.ExecutarAsync(CancellationToken.None);
        var segundoDisparo = await handler.ExecutarAsync(CancellationToken.None);

        Assert.Equal(1, primeiroDisparo);
        Assert.Equal(0, segundoDisparo);
        Assert.Same(lembrete, Assert.Single(notificador.Notificados));
        Assert.Equal(StatusLembreteTarefa.Notificado, lembrete.Status);
        Assert.Equal(agora, lembrete.NotificadoEm);
    }

    private sealed class LembreteRepositoryFake(params LembreteTarefa[] itens)
        : ILembreteTarefaRepository
    {
        public List<LembreteTarefa> Itens { get; } = [.. itens];

        public Task SalvarAsync(LembreteTarefa lembrete, CancellationToken cancellationToken)
        {
            var indice = Itens.FindIndex(item => item.Id == lembrete.Id);
            if (indice >= 0)
            {
                Itens[indice] = lembrete;
            }
            else
            {
                Itens.Add(lembrete);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LembreteTarefa>> ListarPendentesAteAsync(
            DateTimeOffset limite,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LembreteTarefa>>(
                Itens.Where(item =>
                        item.Status == StatusLembreteTarefa.Pendente &&
                        item.LembrarEm <= limite)
                    .ToArray());
    }

    private sealed class NotificadorFake : INotificadorLembrete
    {
        public List<LembreteTarefa> Notificados { get; } = [];

        public Task NotificarAsync(LembreteTarefa lembrete, CancellationToken cancellationToken)
        {
            Notificados.Add(lembrete);
            return Task.CompletedTask;
        }
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}

using Anamnesis.Worker;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class ModoRetencaoWorkerOptionsTests
{
    [Fact]
    public void DeveInterpretarSimulacaoComReuniaoEAgora()
    {
        var reuniaoId = Guid.NewGuid();

        var options = ModoRetencaoWorkerOptions.Interpretar([
            "--retencao-simular",
            "--reuniao", reuniaoId.ToString(),
            "--agora", "2026-08-12T13:34:46Z"
        ]);

        Assert.NotNull(options);
        Assert.False(options!.Aplicar);
        Assert.Equal(reuniaoId, options.ReuniaoId);
        Assert.Equal(new DateTimeOffset(2026, 8, 12, 13, 34, 46, TimeSpan.Zero), options.Agora);
    }

    [Fact]
    public void DeveExigirConfirmacaoParaAplicar()
    {
        var argumentos = new[]
        {
            "--retencao-aplicar",
            "--reuniao", Guid.NewGuid().ToString(),
            "--agora", "2026-08-12T13:34:46Z"
        };

        var excecao = Assert.Throws<ArgumentException>(() => ModoRetencaoWorkerOptions.Interpretar(argumentos));

        Assert.Contains("--confirmar-lixeira", excecao.Message);
    }

    [Fact]
    public void DeveInterpretarAplicacaoConfirmada()
    {
        var reuniaoId = Guid.NewGuid();

        var options = ModoRetencaoWorkerOptions.Interpretar([
            "--retencao-aplicar",
            "--reuniao", reuniaoId.ToString(),
            "--agora", "2026-08-12T13:34:46Z",
            "--confirmar-lixeira"
        ]);

        Assert.NotNull(options);
        Assert.True(options!.Aplicar);
    }
}

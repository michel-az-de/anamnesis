using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class ModoValidacaoTrayOptionsTests
{
    [Fact]
    public void DeveManterModoBandejaSemArgumentos()
    {
        Assert.Null(ModoValidacaoTrayOptions.Obter([]));
    }

    [Fact]
    public void DeveLerDuracaoPositiva()
    {
        var options = ModoValidacaoTrayOptions.Obter(["--gravar-teste-segundos", "3"]);

        Assert.Equal(TimeSpan.FromSeconds(3), options!.Duracao);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("invalido")]
    public void DeveRejeitarDuracaoInvalida(string valor)
    {
        var excecao = Assert.Throws<ArgumentException>(() =>
            ModoValidacaoTrayOptions.Obter(["--gravar-teste-segundos", valor]));

        Assert.Equal("A duração da gravação de teste deve ser um número inteiro positivo de segundos.", excecao.Message);
    }
}

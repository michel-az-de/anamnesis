using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class EdicaoReuniaoDesktopTests
{
    [Fact]
    public void AtualizacaoRemotaNaoDeveSobrescreverRascunhoSujo()
    {
        var estado = new EdicaoReuniaoDesktop();
        estado.Iniciar(Guid.NewGuid(), "Título original", "Texto original");
        estado.Alterar("Título digitado", "Texto sendo corrigido");

        estado.Sincronizar("Título do polling", "Texto do polling");

        Assert.True(estado.Sujo);
        Assert.Equal("Título digitado", estado.Titulo);
        Assert.Equal("Texto sendo corrigido", estado.Transcricao);
    }

    [Fact]
    public void CancelarDeveRestaurarValoresOriginais()
    {
        var estado = new EdicaoReuniaoDesktop();
        estado.Iniciar(Guid.NewGuid(), "Original", "Transcrição original");
        estado.Alterar("Alterado", "Transcrição alterada");

        estado.Cancelar();

        Assert.False(estado.Editando);
        Assert.False(estado.Sujo);
        Assert.Equal("Original", estado.Titulo);
        Assert.Equal("Transcrição original", estado.Transcricao);
    }
}

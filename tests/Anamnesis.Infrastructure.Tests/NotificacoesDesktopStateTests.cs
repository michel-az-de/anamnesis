using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class NotificacoesDesktopStateTests
{
    [Fact]
    public void DeveNotificarSomenteTransicoesNovasDeConclusaoEFalha()
    {
        var reuniao = CriarReuniao("Transcrevendo");
        var state = new NotificacoesDesktopState();

        Assert.Empty(state.Observar([reuniao]));

        reuniao.Status = "Ata pronta";
        var conclusao = Assert.Single(state.Observar([reuniao]));
        Assert.Equal(TipoNotificacaoDesktop.ProcessamentoConcluido, conclusao.Tipo);
        Assert.Equal(reuniao.Id, conclusao.ReuniaoId);
        Assert.Empty(state.Observar([reuniao]));

        reuniao.Status = "Falha";
        var falha = Assert.Single(state.Observar([reuniao]));
        Assert.Equal(TipoNotificacaoDesktop.Falha, falha.Tipo);
        Assert.DoesNotContain("segredo", falha.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", falha.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.Observar([reuniao]));
    }

    [Fact]
    public void NaoDeveNotificarHistoricoAoIniciarAplicativo()
    {
        var state = new NotificacoesDesktopState();

        var notificacoes = state.Observar(
        [
            CriarReuniao("Ata pronta"),
            CriarReuniao("Falha")
        ]);

        Assert.Empty(notificacoes);
    }

    private static ReuniaoDesktopPoc CriarReuniao(string status) => new()
    {
        Id = Guid.NewGuid(),
        Titulo = "Planejamento da alpha",
        Data = "Agora",
        Plataforma = "Captura OBS",
        Duracao = "1 min",
        Status = status,
        Resumo = "Resumo seguro",
        PontosPrincipais = [],
        Transcricao = [],
        Decisoes = [],
        Tarefas = []
    };
}

using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class GravacaoAutomaticaWidgetTests
{
    [Fact]
    public void DeveExibirGravacaoAutomaticaSemRoubarBarraDeTarefas()
    {
        ExecutarEmSta(() =>
        {
            var aberturas = 0;
            var info = new GravacaoAutomaticaInfo(
                Guid.NewGuid(),
                "Planejamento da alpha",
                "Google Meet");
            using var widget = new GravacaoAutomaticaWidget(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                info,
                () => TimeSpan.FromSeconds(65),
                () => aberturas++,
                () => Task.CompletedTask);

            widget.Show();
            System.Windows.Forms.Application.DoEvents();
            widget.AtualizarAgora();
            CapturarQuandoSolicitado(widget);

            Assert.True(widget.TopMost);
            Assert.False(widget.ShowInTaskbar);
            Assert.True(widget.ExibeSemAtivar);
            Assert.Equal(FormBorderStyle.None, widget.FormBorderStyle);
            Assert.Contains(EncontrarLabels(widget), label => label.Text == "GRAVANDO AUTOMATICAMENTE");
            Assert.Contains(EncontrarLabels(widget), label => label.Text.Contains("Google Meet", StringComparison.Ordinal));
            Assert.Contains(EncontrarLabels(widget), label => label.Text == "01:05");

            EncontrarBotao(widget, "Abrir").PerformClick();
            Assert.Equal(1, aberturas);
        });
    }

    [Fact]
    public void EncerrarDeveExecutarUmaVezEOcultarIndicador()
    {
        ExecutarEmSta(() =>
        {
            var encerramentos = 0;
            using var widget = new GravacaoAutomaticaWidget(
                TemaDesktopPoc.Claro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new GravacaoAutomaticaInfo(Guid.NewGuid(), "Reunião detectada", "Microsoft Teams"),
                () => TimeSpan.Zero,
                () => { },
                () =>
                {
                    encerramentos++;
                    return Task.CompletedTask;
                });
            widget.Show();
            System.Windows.Forms.Application.DoEvents();

            EncontrarBotao(widget, "Encerrar").PerformClick();
            AguardarInterface(() => !widget.Visible);

            Assert.Equal(1, encerramentos);
        });
    }

    private static Button EncontrarBotao(Control raiz, string texto) =>
        EncontrarControles(raiz)
            .OfType<Button>()
            .Single(botao => botao.Text.Contains(texto, StringComparison.Ordinal));

    private static IEnumerable<Label> EncontrarLabels(Control raiz) =>
        EncontrarControles(raiz).OfType<Label>();

    private static IEnumerable<Control> EncontrarControles(Control raiz)
    {
        foreach (Control control in raiz.Controls)
        {
            yield return control;
            foreach (var filho in EncontrarControles(control))
            {
                yield return filho;
            }
        }
    }

    private static void AguardarInterface(Func<bool> condicao)
    {
        var limite = DateTime.UtcNow.AddSeconds(3);
        while (!condicao() && DateTime.UtcNow < limite)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.True(condicao());
    }

    private static void CapturarQuandoSolicitado(Form form)
    {
        var caminho = Environment.GetEnvironmentVariable("ANAMNESIS_AUTO_WIDGET_SCREENSHOT");
        if (string.IsNullOrWhiteSpace(caminho))
        {
            return;
        }

        caminho = Path.GetFullPath(caminho);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        using var imagem = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(imagem, new Rectangle(Point.Empty, form.ClientSize));
        imagem.Save(caminho, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void ExecutarEmSta(Action acao)
    {
        Exception? falha = null;
        var thread = new Thread(() =>
        {
            try
            {
                acao();
            }
            catch (Exception exception)
            {
                falha = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (falha is not null)
        {
            throw falha;
        }
    }
}

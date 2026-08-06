using System.Runtime.ExceptionServices;
using Anamnesis.Application.Modelos;
using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

[Collection(InterfaceWindowsGrupo.Nome)]
public sealed class DeteccaoPromptFormTests
{
    private static readonly PlataformaLocal Meet = new(
        "browser_meet",
        "Google Meet",
        OrigemPlataformaLocal.Navegador);

    [Fact]
    public void SugestaoDeveSerOpacaETerAsTresAcoesExplicitas()
    {
        ExecutarEmSta(() =>
        {
            using var prompt = new DeteccaoPromptForm(
                null,
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false));

            var resposta = prompt.SugerirInicioAsync(Meet, CancellationToken.None);
            System.Windows.Forms.Application.DoEvents();
            var botoes = EncontrarControles(prompt)
                .OfType<Button>()
                .ToDictionary(botao => botao.AccessibleName ?? string.Empty);

            Assert.Equal(1D, prompt.Opacity);
            Assert.False(prompt.AllowTransparency);
            Assert.Contains("deteccao_iniciar", botoes.Keys);
            Assert.Contains("deteccao_ignorar", botoes.Keys);
            Assert.Contains("deteccao_silenciar", botoes.Keys);

            botoes["deteccao_ignorar"].PerformClick();
            System.Windows.Forms.Application.DoEvents();

            Assert.True(resposta.IsCompletedSuccessfully);
            Assert.Equal(AcaoSugestaoDeteccao.Ignorar, resposta.Result);
        });
    }

    [Fact]
    public void ContagemDeveSerVisivelECancelavel()
    {
        ExecutarEmSta(() =>
        {
            using var prompt = new DeteccaoPromptForm(
                null,
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false));

            prompt.MostrarContagemInicio(Meet, TimeSpan.FromSeconds(3));
            System.Windows.Forms.Application.DoEvents();
            var cancelar = EncontrarControles(prompt)
                .OfType<Button>()
                .Single(botao => botao.AccessibleName == "deteccao_cancelar");

            Assert.Contains(
                EncontrarControles(prompt).OfType<Label>(),
                label => label.Text.Contains('3'));
            cancelar.PerformClick();

            Assert.True(prompt.ConsumirCancelamentoInicio());
            Assert.False(prompt.ConsumirCancelamentoInicio());
        });
    }

    [Fact]
    public void FecharPeloXDeveCancelarContagemEAvisoAtivos()
    {
        ExecutarEmSta(() =>
        {
            using var prompt = new DeteccaoPromptForm(
                null,
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false));

            prompt.MostrarContagemInicio(Meet, TimeSpan.FromSeconds(3));
            System.Windows.Forms.Application.DoEvents();
            prompt.Close();
            System.Windows.Forms.Application.DoEvents();
            Assert.True(prompt.ConsumirCancelamentoInicio());

            prompt.MostrarAvisoEncerramento(TimeSpan.FromSeconds(15));
            System.Windows.Forms.Application.DoEvents();
            prompt.Close();
            System.Windows.Forms.Application.DoEvents();
            Assert.True(prompt.ConsumirCancelamentoEncerramento());
        });
    }

    private static List<Control> EncontrarControles(Control raiz)
    {
        var encontrados = new List<Control>();
        foreach (Control controle in raiz.Controls)
        {
            encontrados.Add(controle);
            encontrados.AddRange(EncontrarControles(controle));
        }

        return encontrados;
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
        if (!thread.Join(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("A validação do prompt não terminou em quinze segundos.");
        }

        if (falha is not null)
        {
            ExceptionDispatchInfo.Capture(falha).Throw();
        }
    }
}

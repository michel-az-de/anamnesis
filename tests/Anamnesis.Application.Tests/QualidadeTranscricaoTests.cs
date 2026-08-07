using Anamnesis.Application.Modelos;
using Anamnesis.Application.UseCases;
using Xunit;

namespace Anamnesis.Application.Tests;

public sealed class QualidadeTranscricaoTests
{
    [Fact]
    public void DeveRejeitarTranscricaoCompostaSomentePorMarcadorMusical()
    {
        var transcricao = new TranscricaoGerada(
            string.Join(Environment.NewLine, Enumerable.Repeat("[MÚSICA DE FUNDO]", 914)),
            "pt");

        var excecao = Assert.Throws<TranscricaoBaixaQualidadeException>(() =>
            QualidadeTranscricao.LimparEValidar(transcricao));

        Assert.Contains("gravação foi preservada", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeveRemoverFraseDominanteEManterConteudoDiverso()
    {
        var repeticao = Enumerable.Repeat("É do problema da criança, né?", 35);
        var conteudo = Enumerable.Range(1, 65).Select(indice =>
            $"Ponto confiável {indice}: decisão discutida pela equipe.");
        var transcricao = new TranscricaoGerada(
            string.Join(Environment.NewLine, repeticao.Concat(conteudo)),
            "pt");

        var limpa = QualidadeTranscricao.LimparEValidar(transcricao);

        Assert.DoesNotContain("problema da criança", limpa.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ponto confiável 1", limpa.Texto, StringComparison.Ordinal);
        Assert.Equal(65, limpa.Texto.Split(Environment.NewLine).Length);
    }

    [Fact]
    public void DeveRemoverLinhaComUmaPalavraPropagada()
    {
        var linhaDegenerada = string.Join(' ', Enumerable.Repeat("não", 80));
        var transcricao = new TranscricaoGerada(
            string.Join(Environment.NewLine,
            [
                linhaDegenerada,
                "A equipe definiu o segmento inicial do produto.",
                "Felipe vai preparar o questionário até amanhã."
            ]),
            "pt");

        var limpa = QualidadeTranscricao.LimparEValidar(transcricao);

        Assert.DoesNotContain(linhaDegenerada, limpa.Texto, StringComparison.Ordinal);
        Assert.Contains("questionário", limpa.Texto, StringComparison.Ordinal);
    }
}

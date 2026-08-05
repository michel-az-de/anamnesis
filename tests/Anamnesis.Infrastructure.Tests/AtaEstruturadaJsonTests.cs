using Anamnesis.Infrastructure.Cli;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class AtaEstruturadaJsonTests
{
    [Fact]
    public void DeveConverterJsonValidoEmAtaGerada()
    {
        const string json = """
            {
              "resumoExecutivo": "Aprovar a proposta.",
              "decisoes": ["Enviar até sexta."],
              "tarefas": [{ "descricao": "Preparar proposta.", "responsavel": "Ana", "prazo": "2026-08-08" }]
            }
            """;

        var ata = AtaEstruturadaJson.Converter(json);

        Assert.Equal("Aprovar a proposta.", ata.ResumoExecutivo);
        Assert.Equal(["Enviar até sexta."], ata.Decisoes);
        var tarefa = Assert.Single(ata.Tarefas);
        Assert.Equal("Preparar proposta.", tarefa.Descricao);
        Assert.Equal("Ana", tarefa.Responsavel);
        Assert.Equal(new DateOnly(2026, 8, 8), tarefa.Prazo);
    }

    [Fact]
    public void DeveRejeitarJsonInvalido()
    {
        var excecao = Assert.Throws<InvalidOperationException>(() => AtaEstruturadaJson.Converter("não é JSON"));

        Assert.Equal("A CLI retornou JSON de ata inválido.", excecao.Message);
    }

    [Fact]
    public void DeveRejeitarResumoAusente()
    {
        var excecao = Assert.Throws<InvalidOperationException>(() => AtaEstruturadaJson.Converter("""{ "decisoes": [], "tarefas": [] }"""));

        Assert.Equal("A CLI não retornou o resumo executivo da ata.", excecao.Message);
    }
}

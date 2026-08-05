using Anamnesis.Domain.Entidades;
using Anamnesis.Infrastructure.Arquivos;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DiscoArquivadorTests
{
    [Fact]
    public async Task DeveArquivarAtaMarkdownComResumoDecisoesETarefas()
    {
        var diretorio = Path.Combine(Path.GetTempPath(), $"anamnesis-ata-{Guid.NewGuid():N}");
        try
        {
            var reuniao = new Reuniao(
                Guid.Parse("d0750440-e497-4b0e-afb5-86c9f9555950"),
                "Planejamento",
                new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero));
            reuniao.IniciarGravacao(reuniao.CriadaEm);
            reuniao.FinalizarGravacao("C:\\gravacoes\\planejamento.mp4", reuniao.CriadaEm.AddMinutes(10));
            reuniao.IniciarTranscricao();
            reuniao.RegistrarTranscricao(new Transcricao("Texto", "pt", reuniao.CriadaEm.AddMinutes(11)));
            reuniao.RegistrarAta(new Ata(
                "Resumo validado.",
                ["Adotar SQLite."],
                [new Tarefa("Validar proposta.", "Ana", new DateOnly(2026, 8, 6))],
                reuniao.CriadaEm.AddMinutes(12)));

            await new DiscoArquivador(diretorio).ArquivarAsync(reuniao, CancellationToken.None);

            var caminhoAta = Path.Combine(diretorio, "2026", "08", reuniao.Id.ToString("N"), "ata.md");
            var markdown = await File.ReadAllTextAsync(caminhoAta);
            Assert.Contains("## Resumo executivo", markdown);
            Assert.Contains("Resumo validado.", markdown);
            Assert.Contains("## Decisões", markdown);
            Assert.Contains("- Adotar SQLite.", markdown);
            Assert.Contains("## Tarefas", markdown);
            Assert.Contains("- [ ] Validar proposta. | Responsável: Ana | Prazo: 2026-08-06", markdown);
        }
        finally
        {
            if (Directory.Exists(diretorio))
            {
                Directory.Delete(diretorio, recursive: true);
            }
        }
    }
}

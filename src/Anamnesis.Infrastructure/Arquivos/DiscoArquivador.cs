using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;
using System.Globalization;
using System.Text;

namespace Anamnesis.Infrastructure.Arquivos;

public sealed class DiscoArquivador(string diretorioRaiz) : IArquivador
{
    private readonly string _diretorioRaiz = Path.GetFullPath(diretorioRaiz);

    public async Task<ArtefatosReuniao> ArquivarAsync(
        Reuniao reuniao,
        CancellationToken cancellationToken)
    {
        var diretorioReuniao = Path.Combine(
            _diretorioRaiz,
            reuniao.CriadaEm.ToString("yyyy", CultureInfo.InvariantCulture),
            reuniao.CriadaEm.ToString("MM", CultureInfo.InvariantCulture),
            reuniao.Id.ToString("N"));

        Directory.CreateDirectory(diretorioReuniao);

        var conteudoAta = RenderizarAta(reuniao);

        var caminhoAta = Path.Combine(diretorioReuniao, "ata.md");
        var caminhoTranscricao = Path.Combine(diretorioReuniao, "transcricao.md");
        await File.WriteAllTextAsync(
            caminhoAta,
            conteudoAta,
            cancellationToken);

        await File.WriteAllTextAsync(
            caminhoTranscricao,
            reuniao.Transcricao!.Texto,
            cancellationToken);

        return new ArtefatosReuniao(
            reuniao.Id,
            diretorioReuniao,
            caminhoAta,
            caminhoTranscricao);
    }

    private static string RenderizarAta(Reuniao reuniao)
    {
        var ata = reuniao.Ata!;
        var markdown = new StringBuilder()
            .Append("# ").AppendLine(reuniao.Titulo)
            .AppendLine()
            .AppendLine("## Resumo executivo")
            .AppendLine(ata.ResumoExecutivo)
            .AppendLine()
            .AppendLine("## Decisões");

        if (ata.Decisoes.Count == 0)
        {
            markdown.AppendLine("- Nenhuma decisão registrada.");
        }
        else
        {
            foreach (var decisao in ata.Decisoes)
            {
                markdown.Append("- ").AppendLine(decisao);
            }
        }

        markdown.AppendLine().AppendLine("## Tarefas");
        if (ata.Tarefas.Count == 0)
        {
            markdown.AppendLine("- Nenhuma tarefa registrada.");
        }
        else
        {
            foreach (var tarefa in ata.Tarefas)
            {
                var responsavel = string.IsNullOrWhiteSpace(tarefa.Responsavel)
                    ? "Não informado"
                    : tarefa.Responsavel;
                var prazo = tarefa.Prazo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    ?? "Não informado";
                markdown
                    .Append("- [ ] ").Append(tarefa.Descricao)
                    .Append(" | Responsável: ").Append(responsavel)
                    .Append(" | Prazo: ").AppendLine(prazo);
            }
        }

        return markdown.ToString();
    }
}

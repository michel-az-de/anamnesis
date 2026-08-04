using Anamnesis.Application.Contracts;
using Anamnesis.Domain.Entidades;
using System.Globalization;

namespace Anamnesis.Infrastructure.Arquivos;

public sealed class DiscoArquivador(string diretorioRaiz) : IArquivador
{
    public async Task ArquivarAsync(Reuniao reuniao, CancellationToken cancellationToken)
    {
        var diretorioReuniao = Path.Combine(
            diretorioRaiz,
            reuniao.CriadaEm.ToString("yyyy", CultureInfo.InvariantCulture),
            reuniao.CriadaEm.ToString("MM", CultureInfo.InvariantCulture),
            reuniao.Id.ToString("N"));

        Directory.CreateDirectory(diretorioReuniao);

        var conteudoAta = $"# {reuniao.Titulo}{Environment.NewLine}{Environment.NewLine}" +
                          $"## Resumo executivo{Environment.NewLine}{reuniao.Ata!.ResumoExecutivo}{Environment.NewLine}";

        await File.WriteAllTextAsync(
            Path.Combine(diretorioReuniao, "ata.md"),
            conteudoAta,
            cancellationToken);

        await File.WriteAllTextAsync(
            Path.Combine(diretorioReuniao, "transcricao.md"),
            reuniao.Transcricao!.Texto,
            cancellationToken);
    }
}

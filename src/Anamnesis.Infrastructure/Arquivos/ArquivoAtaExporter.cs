using System.Globalization;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;

namespace Anamnesis.Infrastructure.Arquivos;

public sealed class ArquivoAtaExporter : IExportadorAta
{
    public async Task<string> ExportarAsync(
        ReuniaoDetalhe detalhe,
        FormatoExportacaoAta formato,
        string caminhoDestino,
        bool sobrescrever,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detalhe);
        var extensaoEsperada = formato switch
        {
            FormatoExportacaoAta.Pdf => ".pdf",
            FormatoExportacaoAta.Docx => ".docx",
            _ => throw new ArgumentOutOfRangeException(nameof(formato))
        };
        if (!string.Equals(Path.GetExtension(caminhoDestino), extensaoEsperada, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"O arquivo precisa usar a extensão {extensaoEsperada}.");
        }

        var destino = Path.GetFullPath(caminhoDestino);
        var diretorio = Path.GetDirectoryName(destino)
            ?? throw new InvalidOperationException("O diretório de destino é inválido.");
        Directory.CreateDirectory(diretorio);
        if (File.Exists(destino) && !sobrescrever)
        {
            throw new IOException("O arquivo já existe e a substituição não foi confirmada.");
        }

        var temporario = Path.Combine(
            diretorio,
            $".{Path.GetFileName(destino)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var documento = AtaDocumento.Criar(detalhe);
            var bytes = formato == FormatoExportacaoAta.Pdf
                ? PdfAtaRenderer.Renderizar(documento)
                : DocxAtaRenderer.Renderizar(documento);
            await File.WriteAllBytesAsync(temporario, bytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporario, destino, overwrite: sobrescrever);
            return destino;
        }
        finally
        {
            if (File.Exists(temporario))
            {
                File.Delete(temporario);
            }
        }
    }
}

internal sealed record AtaDocumento(
    Guid ReuniaoId,
    string Titulo,
    string Data,
    string Duracao,
    string Resumo,
    IReadOnlyList<string> Decisoes,
    IReadOnlyList<string> Tarefas)
{
    public static AtaDocumento Criar(ReuniaoDetalhe detalhe)
    {
        var duracao = detalhe.GravacaoIniciadaEm is null || detalhe.GravacaoFinalizadaEm is null
            ? "Não informada"
            : FormatarDuracao(detalhe.GravacaoFinalizadaEm.Value - detalhe.GravacaoIniciadaEm.Value);
        var tarefas = detalhe.Tarefas.Select(tarefa =>
        {
            var responsavel = string.IsNullOrWhiteSpace(tarefa.Responsavel)
                ? "Responsável não informado"
                : tarefa.Responsavel;
            var prazo = tarefa.Prazo?.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"))
                ?? "sem prazo";
            return $"{tarefa.Descricao} | {responsavel} | {prazo}";
        }).ToArray();

        return new AtaDocumento(
            detalhe.Id,
            detalhe.Titulo,
            detalhe.CriadaEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR")),
            duracao,
            detalhe.ResumoExecutivo!,
            detalhe.Decisoes,
            tarefas);
    }

    private static string FormatarDuracao(TimeSpan duracao)
    {
        if (duracao < TimeSpan.Zero)
        {
            duracao = TimeSpan.Zero;
        }

        return $"{(int)duracao.TotalHours:00}:{duracao.Minutes:00}:{duracao.Seconds:00}";
    }
}

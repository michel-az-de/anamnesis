using System.IO.Compression;
using System.Text;
using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Arquivos;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class AtaDocumentoExporterTests : IDisposable
{
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-export-{Guid.NewGuid():N}");

    [Fact]
    public async Task DeveGerarPdfLocalValidoComAcentosEConteudoEstruturado()
    {
        Directory.CreateDirectory(_diretorio);
        var caminho = Path.Combine(_diretorio, "ata.pdf");

        await new ArquivoAtaExporter().ExportarAsync(
            CriarDetalhe(),
            FormatoExportacaoAta.Pdf,
            caminho,
            sobrescrever: false,
            CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(caminho);
        Assert.Equal("%PDF-1.4", Encoding.ASCII.GetString(bytes, 0, 8));
        Assert.Contains("Planejamento da integração", Encoding.Latin1.GetString(bytes), StringComparison.Ordinal);
        Assert.Contains("Decisões", Encoding.Latin1.GetString(bytes), StringComparison.Ordinal);
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
        CopiarEvidenciaQuandoSolicitado(caminho);
    }

    [Fact]
    public async Task DeveGerarDocxComEstilosListasERodape()
    {
        Directory.CreateDirectory(_diretorio);
        var caminho = Path.Combine(_diretorio, "ata.docx");

        await new ArquivoAtaExporter().ExportarAsync(
            CriarDetalhe(),
            FormatoExportacaoAta.Docx,
            caminho,
            sobrescrever: false,
            CancellationToken.None);

        using var pacote = ZipFile.OpenRead(caminho);
        Assert.NotNull(pacote.GetEntry("[Content_Types].xml"));
        Assert.NotNull(pacote.GetEntry("word/document.xml"));
        Assert.NotNull(pacote.GetEntry("word/styles.xml"));
        Assert.NotNull(pacote.GetEntry("word/numbering.xml"));
        Assert.NotNull(pacote.GetEntry("word/footer1.xml"));
        var documento = await LerEntradaAsync(pacote, "word/document.xml");
        var estilos = await LerEntradaAsync(pacote, "word/styles.xml");
        Assert.Contains("Planejamento da integração", documento, StringComparison.Ordinal);
        Assert.Contains("Resumo executivo", documento, StringComparison.Ordinal);
        Assert.Contains("w:numPr", documento, StringComparison.Ordinal);
        Assert.Contains("standard_business_brief", estilos, StringComparison.Ordinal);
        CopiarEvidenciaQuandoSolicitado(caminho);
    }

    [Fact]
    public async Task NaoDeveSobrescreverArquivoSemConfirmacao()
    {
        Directory.CreateDirectory(_diretorio);
        var caminho = Path.Combine(_diretorio, "ata.pdf");
        await File.WriteAllTextAsync(caminho, "original");

        await Assert.ThrowsAsync<IOException>(() => new ArquivoAtaExporter().ExportarAsync(
            CriarDetalhe(),
            FormatoExportacaoAta.Pdf,
            caminho,
            sobrescrever: false,
            CancellationToken.None));

        Assert.Equal("original", await File.ReadAllTextAsync(caminho));
    }

    [Fact]
    public async Task DevePaginarPdfLongoSemPerderOUltimoItem()
    {
        Directory.CreateDirectory(_diretorio);
        var caminho = Path.Combine(_diretorio, "ata-longa.pdf");
        var decisoes = Enumerable.Range(1, 120)
            .Select(indice => $"Decisão {indice:000}: preservar o contexto completo da reunião para consulta posterior.")
            .ToArray();

        await new ArquivoAtaExporter().ExportarAsync(
            CriarDetalhe() with { Decisoes = decisoes },
            FormatoExportacaoAta.Pdf,
            caminho,
            sobrescrever: false,
            CancellationToken.None);

        var conteudo = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(caminho));
        Assert.DoesNotContain("/Count 1 >>", conteudo, StringComparison.Ordinal);
        Assert.Contains("Decisão 120", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeveRejeitarExtensaoDivergente()
    {
        Directory.CreateDirectory(_diretorio);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ArquivoAtaExporter().ExportarAsync(
            CriarDetalhe(),
            FormatoExportacaoAta.Pdf,
            Path.Combine(_diretorio, "ata.docx"),
            sobrescrever: false,
            CancellationToken.None));
    }

    [Fact]
    public async Task CancelamentoNaoDeveDeixarArquivoFinalOuTemporario()
    {
        Directory.CreateDirectory(_diretorio);
        var caminho = Path.Combine(_diretorio, "ata-cancelada.pdf");
        using var cancelamento = new CancellationTokenSource();
        cancelamento.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ArquivoAtaExporter().ExportarAsync(
            CriarDetalhe(),
            FormatoExportacaoAta.Pdf,
            caminho,
            sobrescrever: false,
            cancelamento.Token));

        Assert.False(File.Exists(caminho));
        Assert.Empty(Directory.EnumerateFiles(_diretorio, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_diretorio))
        {
            Directory.Delete(_diretorio, recursive: true);
        }
    }

    private static async Task<string> LerEntradaAsync(ZipArchive pacote, string nome)
    {
        await using var stream = pacote.GetEntry(nome)!.Open();
        using var leitor = new StreamReader(stream, Encoding.UTF8);
        return await leitor.ReadToEndAsync();
    }

    private static void CopiarEvidenciaQuandoSolicitado(string caminho)
    {
        var diretorio = Environment.GetEnvironmentVariable("ANAMNESIS_EXPORT_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(diretorio))
        {
            return;
        }

        Directory.CreateDirectory(diretorio);
        File.Copy(caminho, Path.Combine(diretorio, Path.GetFileName(caminho)), overwrite: true);
    }

    internal static ReuniaoDetalhe CriarDetalhe()
    {
        var id = Guid.NewGuid();
        var inicio = new DateTimeOffset(2026, 8, 7, 14, 0, 0, TimeSpan.Zero);
        return new ReuniaoDetalhe(
            id,
            "Planejamento da integração",
            inicio,
            StatusReuniao.Arquivada,
            null,
            @"C:\gravacoes\reuniao.mkv",
            inicio,
            inicio.AddMinutes(45),
            "Transcrição local.",
            "pt-BR",
            inicio.AddMinutes(46),
            "A equipe definiu a evolução da ferramenta com segurança e clareza.",
            ["Publicar a versão revisada.", "Preservar a privacidade local."],
            [
                new Tarefa("Enviar o relatório.", "Felipe", new DateOnly(2026, 8, 10)),
                new Tarefa("Validar a entrega.", null, null)
            ],
            inicio.AddMinutes(47),
            inicio.AddMinutes(48),
            null);
    }
}

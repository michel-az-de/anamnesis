using System.Globalization;
using System.Text;

namespace Anamnesis.Infrastructure.Arquivos;

internal static class PdfAtaRenderer
{
    private const double LarguraPagina = 612;
    private const double AlturaPagina = 792;
    private const double Margem = 54;

    public static byte[] Renderizar(AtaDocumento documento)
    {
        var paginas = CriarPaginas(documento);
        var fonteRegularId = 3 + paginas.Count * 2;
        var fonteNegritoId = fonteRegularId + 1;
        using var stream = new MemoryStream();
        Escrever(stream, "%PDF-1.4\n%âãÏÓ\n");
        var offsets = new List<long> { 0 };

        EscreverObjeto(stream, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        var filhos = string.Join(' ', Enumerable.Range(0, paginas.Count).Select(indice => $"{3 + indice * 2} 0 R"));
        EscreverObjeto(stream, offsets, 2, $"<< /Type /Pages /Kids [{filhos}] /Count {paginas.Count} >>");

        for (var indice = 0; indice < paginas.Count; indice++)
        {
            var paginaId = 3 + indice * 2;
            var conteudoId = paginaId + 1;
            EscreverObjeto(
                stream,
                offsets,
                paginaId,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {LarguraPagina:0} {AlturaPagina:0}] " +
                $"/Resources << /Font << /F1 {fonteRegularId} 0 R /F2 {fonteNegritoId} 0 R >> >> /Contents {conteudoId} 0 R >>");
            var conteudo = CriarConteudoPagina(paginas[indice], indice + 1, paginas.Count);
            var tamanho = Encoding.Latin1.GetByteCount(conteudo);
            EscreverObjeto(stream, offsets, conteudoId, $"<< /Length {tamanho} >>\nstream\n{conteudo}endstream");
        }

        EscreverObjeto(stream, offsets, fonteRegularId, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        EscreverObjeto(stream, offsets, fonteNegritoId, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        var inicioXref = stream.Position;
        Escrever(stream, $"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        for (var indice = 1; indice < offsets.Count; indice++)
        {
            Escrever(stream, $"{offsets[indice]:0000000000} 00000 n \n");
        }

        Escrever(stream, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{inicioXref}\n%%EOF\n");
        return stream.ToArray();
    }

    private static List<List<LinhaPdf>> CriarPaginas(AtaDocumento documento)
    {
        var paginas = new List<List<LinhaPdf>> { new() };
        var alturaUsada = 0D;

        void Adicionar(string texto, double tamanho, bool negrito = false, double espacoDepois = 4)
        {
            var larguraCaracteres = Math.Max(24, (int)(96 * 10.5 / tamanho));
            foreach (var linha in QuebrarLinha(texto, larguraCaracteres))
            {
                var altura = tamanho * 1.35;
                if (alturaUsada + altura + 52 > AlturaPagina - 2 * Margem)
                {
                    paginas.Add([]);
                    alturaUsada = 0;
                }

                paginas[^1].Add(new LinhaPdf(linha, tamanho, negrito, alturaUsada));
                alturaUsada += altura;
            }

            alturaUsada += espacoDepois;
        }

        void Secao(string titulo, IReadOnlyList<string> itens)
        {
            Adicionar(titulo, 14, negrito: true, espacoDepois: 7);
            if (itens.Count == 0)
            {
                Adicionar("Nenhum item registrado.", 10.5, espacoDepois: 7);
                return;
            }

            foreach (var item in itens)
            {
                Adicionar($"- {item}", 10.5, espacoDepois: 3);
            }

            alturaUsada += 5;
        }

        Adicionar(documento.Titulo, 22, negrito: true, espacoDepois: 5);
        Adicionar("ATA DE REUNIÃO", 10, negrito: true, espacoDepois: 9);
        Adicionar($"Data: {documento.Data}    Duração: {documento.Duracao}", 9.5, espacoDepois: 16);
        Adicionar("Resumo executivo", 14, negrito: true, espacoDepois: 7);
        Adicionar(documento.Resumo, 10.5, espacoDepois: 13);
        Secao("Decisões", documento.Decisoes);
        Secao("Tarefas", documento.Tarefas);
        return paginas;
    }

    private static string CriarConteudoPagina(IReadOnlyList<LinhaPdf> linhas, int pagina, int totalPaginas)
    {
        var conteudo = new StringBuilder();
        conteudo.AppendLine("0.063 0.090 0.180 rg");
        foreach (var linha in linhas)
        {
            var y = AlturaPagina - Margem - linha.Deslocamento;
            conteudo
                .Append("BT /").Append(linha.Negrito ? "F2" : "F1").Append(' ')
                .Append(linha.Tamanho.ToString("0.0", CultureInfo.InvariantCulture)).Append(" Tf ")
                .Append(Margem.ToString("0.0", CultureInfo.InvariantCulture)).Append(' ')
                .Append(y.ToString("0.0", CultureInfo.InvariantCulture)).Append(" Td (")
                .Append(EscaparPdf(linha.Texto)).AppendLine(") Tj ET");
        }

        conteudo.AppendLine("0.30 0.36 0.45 rg");
        conteudo
            .Append("BT /F1 8.5 Tf ")
            .Append(Margem.ToString("0.0", CultureInfo.InvariantCulture)).Append(" 30 Td (Anamnesis | Página ")
            .Append(pagina).Append(" de ").Append(totalPaginas).AppendLine(") Tj ET");
        return conteudo.ToString();
    }

    private static List<string> QuebrarLinha(string texto, int limite)
    {
        var palavras = texto.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var linhas = new List<string>();
        var atual = new StringBuilder();
        foreach (var palavraOriginal in palavras)
        {
            var palavra = palavraOriginal;
            while (palavra.Length > limite)
            {
                if (atual.Length > 0)
                {
                    linhas.Add(atual.ToString());
                    atual.Clear();
                }

                linhas.Add(palavra[..limite]);
                palavra = palavra[limite..];
            }

            if (atual.Length > 0 && atual.Length + 1 + palavra.Length > limite)
            {
                linhas.Add(atual.ToString());
                atual.Clear();
            }

            if (atual.Length > 0)
            {
                atual.Append(' ');
            }
            atual.Append(palavra);
        }

        if (atual.Length > 0)
        {
            linhas.Add(atual.ToString());
        }

        return linhas.Count == 0 ? [string.Empty] : linhas;
    }

    private static string EscaparPdf(string texto) => texto
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal);

    private static void EscreverObjeto(
        Stream stream,
        List<long> offsets,
        int id,
        string conteudo)
    {
        if (id != offsets.Count)
        {
            throw new InvalidOperationException("A sequência de objetos PDF é inválida.");
        }

        offsets.Add(stream.Position);
        Escrever(stream, $"{id} 0 obj\n{conteudo}\nendobj\n");
    }

    private static void Escrever(Stream stream, string valor)
    {
        var bytes = Encoding.Latin1.GetBytes(valor);
        stream.Write(bytes, 0, bytes.Length);
    }

    private sealed record LinhaPdf(string Texto, double Tamanho, bool Negrito, double Deslocamento);
}

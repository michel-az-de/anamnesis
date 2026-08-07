using System.IO.Compression;
using System.Security;
using System.Text;
using System.Globalization;

namespace Anamnesis.Infrastructure.Arquivos;

internal static class DocxAtaRenderer
{
    public static byte[] Renderizar(AtaDocumento documento)
    {
        using var stream = new MemoryStream();
        using (var pacote = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Adicionar(pacote, "[Content_Types].xml", ContentTypes);
            Adicionar(pacote, "_rels/.rels", PackageRelationships);
            Adicionar(pacote, "docProps/core.xml", CoreProperties(documento));
            Adicionar(pacote, "docProps/app.xml", AppProperties);
            Adicionar(pacote, "word/_rels/document.xml.rels", DocumentRelationships);
            Adicionar(pacote, "word/styles.xml", Styles);
            Adicionar(pacote, "word/numbering.xml", Numbering);
            Adicionar(pacote, "word/footer1.xml", Footer);
            Adicionar(pacote, "word/document.xml", Document(documento));
        }

        return stream.ToArray();
    }

    private static string Document(AtaDocumento documento)
    {
        var corpo = new StringBuilder()
            .Append(Paragrafo(documento.Titulo, "Title"))
            .Append(Paragrafo("Ata de reunião", "Subtitle"))
            .Append(Paragrafo($"Data: {documento.Data}    Duração: {documento.Duracao}", "Metadata"))
            .Append(Paragrafo("Resumo executivo", "Heading1"))
            .Append(Paragrafo(documento.Resumo, "Normal"))
            .Append(Paragrafo("Decisões", "Heading1"));
        AdicionarLista(corpo, documento.Decisoes, "Nenhuma decisão registrada.");
        corpo.Append(Paragrafo("Tarefas", "Heading1"));
        AdicionarLista(corpo, documento.Tarefas, "Nenhuma tarefa registrada.");

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <w:body>
                {corpo}
                <w:sectPr>
                  <w:footerReference w:type="default" r:id="rId3"/>
                  <w:pgSz w:w="12240" w:h="15840"/>
                  <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708" w:gutter="0"/>
                </w:sectPr>
              </w:body>
            </w:document>
            """;
    }

    private static void AdicionarLista(StringBuilder corpo, IReadOnlyList<string> itens, string vazio)
    {
        if (itens.Count == 0)
        {
            corpo.Append(Paragrafo(vazio, "Normal"));
            return;
        }

        foreach (var item in itens)
        {
            corpo.Append(CultureInfo.InvariantCulture, $"""
                <w:p>
                  <w:pPr><w:pStyle w:val="ListParagraph"/><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr></w:pPr>
                  <w:r><w:t xml:space="preserve">{Escapar(item)}</w:t></w:r>
                </w:p>
                """);
        }
    }

    private static string Paragrafo(string texto, string estilo) => $"""
        <w:p>
          <w:pPr><w:pStyle w:val="{estilo}"/></w:pPr>
          <w:r><w:t xml:space="preserve">{Escapar(texto)}</w:t></w:r>
        </w:p>
        """;

    private static string CoreProperties(AtaDocumento documento) => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <dc:title>{Escapar(documento.Titulo)}</dc:title>
          <dc:creator>Anamnesis</dc:creator>
          <cp:lastModifiedBy>Anamnesis</cp:lastModifiedBy>
          <dcterms:created xsi:type="dcterms:W3CDTF">2026-08-07T00:00:00Z</dcterms:created>
          <dcterms:modified xsi:type="dcterms:W3CDTF">2026-08-07T00:00:00Z</dcterms:modified>
        </cp:coreProperties>
        """;

    private static void Adicionar(ZipArchive pacote, string nome, string conteudo)
    {
        var entrada = pacote.CreateEntry(nome, CompressionLevel.Optimal);
        using var stream = entrada.Open();
        using var escritor = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        escritor.Write(conteudo);
    }

    private static string Escapar(string valor) => SecurityElement.Escape(valor) ?? string.Empty;

    private const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
          <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
          <Override PartName="/word/numbering.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/>
          <Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
          <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
          <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
        </Types>
        """;

    private const string PackageRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
        </Relationships>
        """;

    private const string DocumentRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering" Target="numbering.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>
        </Relationships>
        """;

    private const string Styles = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="22"/><w:color w:val="172033"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="120" w:line="264" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>
          <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:pPr><w:spacing w:after="120" w:line="264" w:lineRule="auto"/></w:pPr><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="22"/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:before="0" w:after="80"/></w:pPr><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:b/><w:sz w:val="48"/><w:color w:val="10172E"/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="Subtitle"><w:name w:val="Subtitle"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="160"/></w:pPr><w:rPr><w:b/><w:sz w:val="22"/><w:color w:val="B87333"/><w:caps/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="Metadata"><w:name w:val="Metadata"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="240"/></w:pPr><w:rPr><w:sz w:val="19"/><w:color w:val="4D596A"/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="320" w:after="160"/><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:sz w:val="32"/><w:color w:val="2E5C72"/></w:rPr></w:style>
          <w:style w:type="paragraph" w:styleId="ListParagraph"><w:name w:val="List Paragraph"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="160" w:line="280" w:lineRule="auto"/><w:ind w:left="720" w:hanging="360"/></w:pPr></w:style>
          <w:style w:type="paragraph" w:styleId="AnamnesisPreset"><w:name w:val="standard_business_brief"/><w:basedOn w:val="Normal"/></w:style>
        </w:styles>
        """;

    private const string Numbering = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:numbering xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:abstractNum w:abstractNumId="0"><w:multiLevelType w:val="singleLevel"/><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="•"/><w:lvlJc w:val="left"/><w:pPr><w:tabs><w:tab w:val="num" w:pos="720"/></w:tabs><w:ind w:left="720" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/></w:rPr></w:lvl></w:abstractNum>
          <w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num>
        </w:numbering>
        """;

    private const string Footer = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:p><w:pPr><w:jc w:val="right"/></w:pPr><w:r><w:rPr><w:sz w:val="17"/><w:color w:val="667085"/></w:rPr><w:t xml:space="preserve">Anamnesis | Página </w:t></w:r><w:fldSimple w:instr=" PAGE "><w:r><w:rPr><w:sz w:val="17"/><w:color w:val="667085"/></w:rPr><w:t>1</w:t></w:r></w:fldSimple></w:p></w:ftr>
        """;

    private const string AppProperties = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"><Application>Anamnesis</Application><AppVersion>1.0</AppVersion></Properties>
        """;
}

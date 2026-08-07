using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;

namespace Anamnesis.Infrastructure.Arquivos;

public sealed partial class ObsidianPublisher : IPublicadorObsidian
{
    private readonly Func<string, FileAttributes> _obterAtributos;
    private readonly Action<string>? _antesDaValidacaoFinal;

    public ObsidianPublisher()
        : this(File.GetAttributes, null)
    {
    }

    internal ObsidianPublisher(
        Func<string, FileAttributes> obterAtributos,
        Action<string>? antesDaValidacaoFinal)
    {
        _obterAtributos = obterAtributos;
        _antesDaValidacaoFinal = antesDaValidacaoFinal;
    }

    public async Task<string> PublicarAsync(
        ReuniaoDetalhe detalhe,
        string caminhoVault,
        string subpasta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detalhe);
        var vault = Path.GetFullPath(caminhoVault);
        if (!Directory.Exists(vault) || !Directory.Exists(Path.Combine(vault, ".obsidian")))
        {
            throw new InvalidOperationException("A pasta escolhida não é um vault válido do Obsidian.");
        }

        var segmentos = NormalizarSubpasta(subpasta);
        var destinoBase = segmentos.Aggregate(vault, Path.Combine);
        var destino = Path.Combine(
            destinoBase,
            detalhe.CriadaEm.ToString("yyyy", CultureInfo.InvariantCulture),
            detalhe.CriadaEm.ToString("MM", CultureInfo.InvariantCulture));
        GarantirDentroDoVault(vault, destino);
        GarantirSemReparsePoint(vault, destino);
        Directory.CreateDirectory(destino);
        GarantirSemReparsePoint(vault, destino);

        var nome = $"{detalhe.CriadaEm:yyyy-MM-dd}-{CriarSlug(detalhe.Titulo)}-{detalhe.Id.ToString("N")[..8]}.md";
        var caminhoFinal = Path.Combine(destino, nome);
        if (File.Exists(caminhoFinal))
        {
            return await ValidarExistenteAsync(caminhoFinal, detalhe.Id, cancellationToken);
        }

        var temporario = Path.Combine(destino, $".{nome}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(
                temporario,
                RenderizarMarkdown(detalhe),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            _antesDaValidacaoFinal?.Invoke(destino);
            GarantirSemReparsePoint(vault, destino);
            try
            {
                File.Move(temporario, caminhoFinal);
            }
            catch (IOException) when (File.Exists(caminhoFinal))
            {
                return await ValidarExistenteAsync(caminhoFinal, detalhe.Id, cancellationToken);
            }

            return caminhoFinal;
        }
        finally
        {
            if (File.Exists(temporario))
            {
                File.Delete(temporario);
            }
        }
    }

    private static string[] NormalizarSubpasta(string subpasta)
    {
        var segmentos = (subpasta ?? string.Empty)
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segmentos.Length == 0 || segmentos.Any(segmento =>
                segmento is "." or ".." ||
                string.Equals(segmento, ".obsidian", StringComparison.OrdinalIgnoreCase) ||
                segmento.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidOperationException("A subpasta escolhida não é segura para publicação.");
        }

        return segmentos;
    }

    private static void GarantirDentroDoVault(string vault, string destino)
    {
        var prefixo = vault.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!destino.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("O destino precisa permanecer dentro do vault.");
        }
    }

    private void GarantirSemReparsePoint(string vault, string destino)
    {
        var atual = vault;
        VerificarDiretorio(atual);
        var relativo = Path.GetRelativePath(vault, destino);
        foreach (var segmento in relativo.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            atual = Path.Combine(atual, segmento);
            if (Directory.Exists(atual))
            {
                VerificarDiretorio(atual);
            }
        }
    }

    private void VerificarDiretorio(string caminho)
    {
        if ((_obterAtributos(caminho) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("O destino contém redirecionamento de sistema de arquivos.");
        }
    }

    private static async Task<string> ValidarExistenteAsync(
        string caminho,
        Guid reuniaoId,
        CancellationToken cancellationToken)
    {
        var conteudo = await File.ReadAllTextAsync(caminho, cancellationToken);
        if (!conteudo.Contains($"anamnesis_id: {reuniaoId:N}", StringComparison.Ordinal))
        {
            throw new IOException("Já existe uma nota diferente com o mesmo nome.");
        }

        return caminho;
    }

    private static string RenderizarMarkdown(ReuniaoDetalhe detalhe)
    {
        var markdown = new StringBuilder()
            .AppendLine("---")
            .Append("anamnesis_id: ").AppendLine(detalhe.Id.ToString("N"))
            .Append("titulo: ").AppendLine(JsonSerializer.Serialize(detalhe.Titulo))
            .Append("data: ").AppendLine(detalhe.CriadaEm.ToString("O", CultureInfo.InvariantCulture))
            .AppendLine("status: arquivada")
            .AppendLine("origem: Anamnesis")
            .AppendLine("tags:")
            .AppendLine("  - anamnesis")
            .AppendLine("  - reuniao")
            .AppendLine("---")
            .AppendLine()
            .Append("# ").AppendLine(SanitizarConteudo(detalhe.Titulo))
            .AppendLine()
            .AppendLine("## Resumo executivo")
            .AppendLine(SanitizarConteudo(detalhe.ResumoExecutivo!))
            .AppendLine()
            .AppendLine("## Decisões");
        AdicionarLista(markdown, detalhe.Decisoes, "Nenhuma decisão registrada.", tarefa: false);
        markdown.AppendLine().AppendLine("## Tarefas");
        if (detalhe.Tarefas.Count == 0)
        {
            markdown.AppendLine("Nenhuma tarefa registrada.");
        }
        else
        {
            foreach (var tarefa in detalhe.Tarefas)
            {
                var responsavel = string.IsNullOrWhiteSpace(tarefa.Responsavel)
                    ? "Não informado"
                    : SanitizarConteudo(tarefa.Responsavel);
                var prazo = tarefa.Prazo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "Não informado";
                markdown.Append("- [ ] ").Append(SanitizarConteudo(tarefa.Descricao))
                    .Append(" | Responsável: ").Append(responsavel)
                    .Append(" | Prazo: ").AppendLine(prazo);
            }
        }

        return markdown.ToString();
    }

    private static void AdicionarLista(
        StringBuilder markdown,
        IReadOnlyList<string> itens,
        string vazio,
        bool tarefa)
    {
        if (itens.Count == 0)
        {
            markdown.AppendLine(vazio);
            return;
        }

        foreach (var item in itens)
        {
            markdown.Append(tarefa ? "- [ ] " : "- ").AppendLine(SanitizarConteudo(item));
        }
    }

    private static string SanitizarConteudo(string valor)
    {
        var semRecursos = RecursoRemotoRegex().Replace(valor, "[recurso remoto removido]");
        return semRecursos.Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string CriarSlug(string titulo)
    {
        var caracteres = titulo.Trim().ToLowerInvariant()
            .Select(caractere => char.IsLetterOrDigit(caractere) ? caractere : '-')
            .ToArray();
        var slug = HifensRepetidosRegex().Replace(new string(caracteres), "-").Trim('-');
        if (slug.Length == 0)
        {
            slug = "reuniao";
        }

        return slug.Length <= 56 ? slug : slug[..56].TrimEnd('-');
    }

    [GeneratedRegex(@"!\[[^\]]*\]\(https?://[^\)]*\)|!\[\[https?://[^\]]*\]\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecursoRemotoRegex();

    [GeneratedRegex("-+", RegexOptions.CultureInvariant)]
    private static partial Regex HifensRepetidosRegex();
}

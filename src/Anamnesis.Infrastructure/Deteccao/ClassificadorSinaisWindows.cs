using Anamnesis.Application.Modelos;
using Anamnesis.Infrastructure.Configuracao;

namespace Anamnesis.Infrastructure.Deteccao;

internal sealed record JanelaWindows(string FamiliaProcesso, string Titulo);

internal sealed record LeituraSinaisWindows(
    IReadOnlyCollection<string> FamiliasComCaptura,
    IReadOnlyCollection<string> FamiliasComRenderizacao,
    IReadOnlyCollection<JanelaWindows> Janelas);

internal sealed class ClassificadorSinaisWindows(DeteccaoLocalOptions options)
{
    public SinaisDeteccaoReuniao Classificar(LeituraSinaisWindows leitura)
    {
        ArgumentNullException.ThrowIfNull(leitura);
        var capturas = Normalizar(leitura.FamiliasComCaptura);
        var renderizacoes = Normalizar(leitura.FamiliasComRenderizacao);
        var candidatos = ClassificarAplicativos(capturas, renderizacoes)
            .Concat(ClassificarNavegadores(capturas, renderizacoes, leitura.Janelas))
            .GroupBy(candidato => candidato.Plataforma.Chave, StringComparer.Ordinal)
            .Select(grupo => new Candidato(
                grupo.First().Plataforma,
                grupo.Any(item => item.MicrofoneAtivo),
                grupo.Any(item => item.AudioSaidaAtivo)))
            .ToArray();

        if (candidatos.Length == 1)
        {
            var candidato = candidatos[0];
            return new SinaisDeteccaoReuniao(
                candidato.MicrofoneAtivo,
                candidato.AudioSaidaAtivo,
                candidato.Plataforma,
                EventoAgendaProximo: false,
                Ambiguo: false);
        }

        return new SinaisDeteccaoReuniao(
            capturas.Count > 0,
            renderizacoes.Count > 0,
            Plataforma: null,
            EventoAgendaProximo: false,
            Ambiguo: candidatos.Length > 1);
    }

    private IEnumerable<Candidato> ClassificarAplicativos(
        HashSet<string> capturas,
        HashSet<string> renderizacoes)
    {
        foreach (var aplicativo in options.Aplicativos ?? [])
        {
            var familias = Normalizar(aplicativo.FamiliasProcesso ?? []);
            var captura = familias.Overlaps(capturas);
            var renderizacao = familias.Overlaps(renderizacoes);
            if (!captura && !renderizacao)
            {
                continue;
            }

            yield return new Candidato(
                new PlataformaLocal(
                    aplicativo.Chave,
                    aplicativo.Nome,
                    OrigemPlataformaLocal.AplicativoNativo),
                captura,
                renderizacao);
        }
    }

    private IEnumerable<Candidato> ClassificarNavegadores(
        HashSet<string> capturas,
        HashSet<string> renderizacoes,
        IReadOnlyCollection<JanelaWindows> janelas)
    {
        var navegadores = Normalizar(options.Navegadores ?? []);
        foreach (var janela in janelas ?? [])
        {
            var familia = Normalizar(janela.FamiliaProcesso);
            if (!navegadores.Contains(familia) ||
                !capturas.Contains(familia) && !renderizacoes.Contains(familia))
            {
                continue;
            }

            var assinatura = (options.JanelasChamada ?? []).FirstOrDefault(configuracao =>
                (configuracao.Assinaturas ?? []).Any(valor =>
                    !string.IsNullOrWhiteSpace(valor) &&
                    janela.Titulo.Contains(valor, StringComparison.OrdinalIgnoreCase)));
            if (assinatura is null)
            {
                continue;
            }

            yield return new Candidato(
                new PlataformaLocal(
                    assinatura.Chave,
                    assinatura.Nome,
                    OrigemPlataformaLocal.Navegador),
                capturas.Contains(familia),
                renderizacoes.Contains(familia));
        }
    }

    private static HashSet<string> Normalizar(IEnumerable<string> familias) =>
        new(
            familias
                .Where(familia => !string.IsNullOrWhiteSpace(familia))
                .Select(Normalizar),
            StringComparer.Ordinal);

    private static string Normalizar(string familia) =>
        Path.GetFileNameWithoutExtension(familia.Trim()).ToLowerInvariant();

    private sealed record Candidato(
        PlataformaLocal Plataforma,
        bool MicrofoneAtivo,
        bool AudioSaidaAtivo);
}

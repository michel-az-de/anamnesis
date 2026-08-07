using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.UseCases;

public sealed class ExportarAtaHandler(
    IReuniaoQuery reuniaoQuery,
    IExportadorAta exportador)
{
    public async Task<string> ExecutarAsync(
        Guid reuniaoId,
        FormatoExportacaoAta formato,
        string caminhoDestino,
        bool sobrescrever,
        CancellationToken cancellationToken)
    {
        if (reuniaoId == Guid.Empty)
        {
            throw new ArgumentException("A reunião é obrigatória.", nameof(reuniaoId));
        }

        if (string.IsNullOrWhiteSpace(caminhoDestino))
        {
            throw new ArgumentException("Escolha um arquivo de destino.", nameof(caminhoDestino));
        }

        var detalhe = await reuniaoQuery.ObterDetalheAsync(reuniaoId, cancellationToken)
            ?? throw new InvalidOperationException("A reunião não foi encontrada no banco local.");
        ValidarAta(detalhe);

        return await exportador.ExportarAsync(
            detalhe,
            formato,
            caminhoDestino,
            sobrescrever,
            cancellationToken);
    }

    internal static void ValidarAta(ReuniaoDetalhe detalhe)
    {
        if (detalhe.AtaGeradaEm is null || string.IsNullOrWhiteSpace(detalhe.ResumoExecutivo))
        {
            throw new InvalidOperationException(
                "A reunião ainda não possui uma ata concluída para exportação.");
        }
    }
}

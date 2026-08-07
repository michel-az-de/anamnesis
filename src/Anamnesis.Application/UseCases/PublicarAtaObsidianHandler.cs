using Anamnesis.Application.Contracts;

namespace Anamnesis.Application.UseCases;

public sealed class PublicarAtaObsidianHandler(
    IReuniaoQuery reuniaoQuery,
    IPublicadorObsidian publicador)
{
    public async Task<string> ExecutarAsync(
        Guid reuniaoId,
        string caminhoVault,
        string subpasta,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(caminhoVault))
        {
            throw new ArgumentException("Escolha um vault do Obsidian.", nameof(caminhoVault));
        }

        var detalhe = await reuniaoQuery.ObterDetalheAsync(reuniaoId, cancellationToken)
            ?? throw new InvalidOperationException("A reunião não foi encontrada no banco local.");
        ExportarAtaHandler.ValidarAta(detalhe);

        return await publicador.PublicarAsync(
            detalhe,
            caminhoVault,
            subpasta,
            cancellationToken);
    }
}

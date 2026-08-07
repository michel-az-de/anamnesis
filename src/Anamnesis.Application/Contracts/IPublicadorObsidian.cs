using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface IPublicadorObsidian
{
    Task<string> PublicarAsync(
        ReuniaoDetalhe detalhe,
        string caminhoVault,
        string subpasta,
        CancellationToken cancellationToken);
}

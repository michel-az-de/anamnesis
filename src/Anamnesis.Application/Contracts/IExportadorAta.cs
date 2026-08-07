using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface IExportadorAta
{
    Task<string> ExportarAsync(
        ReuniaoDetalhe detalhe,
        FormatoExportacaoAta formato,
        string caminhoDestino,
        bool sobrescrever,
        CancellationToken cancellationToken);
}

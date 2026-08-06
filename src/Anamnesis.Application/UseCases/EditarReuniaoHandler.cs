using Anamnesis.Application.Contracts;
using Anamnesis.Domain.Tipos;

namespace Anamnesis.Application.UseCases;

public sealed class EditarReuniaoHandler(
    IReuniaoRepository reuniaoRepository,
    IArquivador arquivador)
{
    public async Task ExecutarAsync(
        Guid reuniaoId,
        string titulo,
        string transcricao,
        CancellationToken cancellationToken)
    {
        var reuniao = await reuniaoRepository.ObterAsync(reuniaoId, cancellationToken)
            ?? throw new InvalidOperationException($"A reunião '{reuniaoId}' não foi encontrada.");

        reuniao.EditarTitulo(titulo);
        reuniao.EditarTranscricao(transcricao);
        await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);

        if (reuniao.Status is StatusReuniao.Arquivada or StatusReuniao.PendenteExclusao)
        {
            await arquivador.ArquivarAsync(reuniao, cancellationToken);
        }
    }
}

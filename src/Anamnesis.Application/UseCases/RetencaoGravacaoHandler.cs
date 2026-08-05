using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;

namespace Anamnesis.Application.UseCases;

public sealed class RetencaoGravacaoHandler(
    IReuniaoRepository reuniaoRepository,
    IGravacaoLixeira lixeira,
    TimeProvider relogio)
{
    private static readonly TimeSpan PrazoPadrao = TimeSpan.FromDays(7);

    public async Task<ResultadoRetencao> SimularAsync(Guid reuniaoId, CancellationToken cancellationToken)
    {
        var reuniao = await ObterReuniaoAsync(reuniaoId, cancellationToken);
        return await AvaliarAsync(reuniao, cancellationToken);
    }

    public async Task AplicarAsync(Guid reuniaoId, CancellationToken cancellationToken)
    {
        var reuniao = await ObterReuniaoAsync(reuniaoId, cancellationToken);
        var resultado = await AvaliarAsync(reuniao, cancellationToken);
        if (!resultado.PodeMover)
        {
            if (resultado.CaminhoArquivo is not null && resultado.Motivo == "A gravação não foi encontrada.")
            {
                throw new FileNotFoundException(resultado.Motivo, resultado.CaminhoArquivo);
            }

            throw new InvalidOperationException(resultado.Motivo);
        }

        reuniao.MarcarPendenteExclusao();
        await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);

        try
        {
            await lixeira.MoverAsync(resultado.CaminhoArquivo!, cancellationToken);
            reuniao.MarcarExcluida();
            await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);
        }
        catch
        {
            reuniao.CancelarExclusaoPendente();
            await reuniaoRepository.SalvarAsync(reuniao, CancellationToken.None);
            throw;
        }
    }

    private async Task<Reuniao> ObterReuniaoAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
        await reuniaoRepository.ObterAsync(reuniaoId, cancellationToken)
        ?? throw new InvalidOperationException($"A reunião '{reuniaoId}' não foi encontrada.");

    private async Task<ResultadoRetencao> AvaliarAsync(Reuniao reuniao, CancellationToken cancellationToken)
    {
        if (reuniao.Status != StatusReuniao.Arquivada)
        {
            return new ResultadoRetencao(false, reuniao.Gravacao?.CaminhoArquivo, "A reunião não está arquivada.");
        }

        if (reuniao.ArquivadaEm is null || relogio.GetUtcNow() - reuniao.ArquivadaEm < PrazoPadrao)
        {
            return new ResultadoRetencao(false, reuniao.Gravacao?.CaminhoArquivo, "O prazo de retenção ainda não foi atingido.");
        }

        var caminhoArquivo = reuniao.Gravacao?.CaminhoArquivo;
        if (string.IsNullOrWhiteSpace(caminhoArquivo) || !await lixeira.ExisteAsync(caminhoArquivo, cancellationToken))
        {
            return new ResultadoRetencao(false, caminhoArquivo, "A gravação não foi encontrada.");
        }

        return new ResultadoRetencao(true, caminhoArquivo, null);
    }
}

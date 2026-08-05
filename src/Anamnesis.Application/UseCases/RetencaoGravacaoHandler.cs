using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;

namespace Anamnesis.Application.UseCases;

public sealed class RetencaoGravacaoHandler(
    IReuniaoRepository reuniaoRepository,
    IGravacaoLixeira lixeira,
    TimeProvider relogio,
    JornalOperacional? journal = null)
{
    private static readonly TimeSpan PrazoPadrao = TimeSpan.FromDays(7);
    private readonly JornalOperacional _journal = journal ?? JornalOperacional.Nulo;

    public async Task<ResultadoRetencao> SimularAsync(Guid reuniaoId, CancellationToken cancellationToken)
    {
        var reuniao = await ObterReuniaoAsync(reuniaoId, cancellationToken);
        return await AvaliarRegistrandoAsync(reuniao, cancellationToken);
    }

    public async Task AplicarAsync(Guid reuniaoId, CancellationToken cancellationToken)
    {
        var reuniao = await ObterReuniaoAsync(reuniaoId, cancellationToken);
        var resultado = await AvaliarRegistrandoAsync(reuniao, cancellationToken);
        if (!resultado.PodeMover)
        {
            if (resultado.Motivo is MotivoRetencao.GravacaoNaoEncontrada &&
                resultado.CaminhoArquivo is not null)
            {
                throw new FileNotFoundException(resultado.Descricao, resultado.CaminhoArquivo);
            }

            throw new InvalidOperationException(resultado.Descricao);
        }

        reuniao.MarcarPendenteExclusao();
        await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);

        try
        {
            await lixeira.MoverAsync(resultado.CaminhoArquivo!, cancellationToken);
            reuniao.MarcarExcluida();
            await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);
            await _journal.RegistrarAsync(
                NivelEventoOperacional.Info,
                CodigosEventoOperacional.RetencaoAplicada,
                "Retencao",
                "Gravação movida para a Lixeira.",
                reuniao.Id,
                null,
                new MetadadosEventoOperacional(
                    Operacao: "aplicar_retencao",
                    Resultado: "sucesso",
                    MotivoCodigo: resultado.Motivo.ToString()),
                cancellationToken);
        }
        catch (Exception exception)
        {
            reuniao.CancelarExclusaoPendente();
            await reuniaoRepository.SalvarAsync(reuniao, CancellationToken.None);
            await _journal.RegistrarFalhaAsync(
                "Retencao",
                "aplicar_retencao",
                exception,
                reuniao.Id,
                null,
                CancellationToken.None);
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
            return new ResultadoRetencao(MotivoRetencao.ReuniaoNaoArquivada, reuniao.Gravacao?.CaminhoArquivo);
        }

        if (reuniao.ArquivadaEm is null || relogio.GetUtcNow() - reuniao.ArquivadaEm < PrazoPadrao)
        {
            return new ResultadoRetencao(MotivoRetencao.PrazoNaoAtingido, reuniao.Gravacao?.CaminhoArquivo);
        }

        var caminhoArquivo = reuniao.Gravacao?.CaminhoArquivo;
        if (string.IsNullOrWhiteSpace(caminhoArquivo) || !await lixeira.ExisteAsync(caminhoArquivo, cancellationToken))
        {
            return new ResultadoRetencao(MotivoRetencao.GravacaoNaoEncontrada, caminhoArquivo);
        }

        return new ResultadoRetencao(MotivoRetencao.Elegivel, caminhoArquivo);
    }

    private async Task<ResultadoRetencao> AvaliarRegistrandoAsync(
        Reuniao reuniao,
        CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await AvaliarAsync(reuniao, cancellationToken);
            await RegistrarAvaliacaoAsync(reuniao, resultado, cancellationToken);
            return resultado;
        }
        catch (Exception exception)
        {
            await _journal.RegistrarFalhaAsync(
                "Retencao",
                "avaliar_retencao",
                exception,
                reuniao.Id,
                null,
                CancellationToken.None);
            throw;
        }
    }

    private Task RegistrarAvaliacaoAsync(
        Reuniao reuniao,
        ResultadoRetencao resultado,
        CancellationToken cancellationToken) =>
        _journal.RegistrarAsync(
            NivelEventoOperacional.Info,
            CodigosEventoOperacional.RetencaoAvaliada,
            "Retencao",
            "Política de retenção avaliada.",
            reuniao.Id,
            null,
            new MetadadosEventoOperacional(
                Operacao: "avaliar_retencao",
                Resultado: resultado.PodeMover ? "elegivel" : "nao_elegivel",
                MotivoCodigo: resultado.Motivo.ToString()),
            cancellationToken);
}

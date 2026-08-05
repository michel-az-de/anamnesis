using Anamnesis.Application.Contracts;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;

namespace Anamnesis.Application.UseCases;

public sealed class ControlarGravacaoHandler(
    IReuniaoRepository reuniaoRepository,
    IJobQueue jobQueue,
    IGravador gravador,
    IWorkerLauncher workerLauncher,
    IObsPreflight obsPreflight,
    TimeProvider relogio)
{
    public async Task<Guid> IniciarAsync(string titulo, CancellationToken cancellationToken)
    {
        var agora = relogio.GetUtcNow();
        var reuniao = new Reuniao(Guid.NewGuid(), titulo, agora);
        reuniao.IniciarGravacao(agora);
        await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);

        try
        {
            await obsPreflight.PrepararAsync(cancellationToken);
            await gravador.IniciarAsync(cancellationToken);
            return reuniao.Id;
        }
        catch (Exception exception)
        {
            reuniao.RegistrarFalha(exception.Message);
            await reuniaoRepository.SalvarAsync(reuniao, CancellationToken.None);
            throw;
        }
    }

    public async Task ReconciliarGravacaoAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken)
    {
        var reuniao = await reuniaoRepository.ObterAsync(reuniaoId, cancellationToken)
            ?? throw new InvalidOperationException($"A reunião '{reuniaoId}' não foi encontrada.");
        if (reuniao.Status != StatusReuniao.Gravando)
        {
            return;
        }

        await obsPreflight.PrepararAsync(cancellationToken);
        if (await gravador.EstaGravandoAsync(cancellationToken))
        {
            return;
        }

        reuniao.RegistrarFalha(
            "A gravação foi interrompida antes da confirmação do OBS e pode ser iniciada novamente.");
        await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);
    }

    public async Task FinalizarAsync(Guid reuniaoId, CancellationToken cancellationToken)
    {
        var reuniao = await reuniaoRepository.ObterAsync(reuniaoId, cancellationToken)
            ?? throw new InvalidOperationException($"A reunião '{reuniaoId}' não foi encontrada.");
        try
        {
            var caminhoArquivo = await gravador.FinalizarAsync(cancellationToken);
            reuniao.FinalizarGravacao(caminhoArquivo, relogio.GetUtcNow());
            await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Sem registrar a falha, a reunião fica presa em 'Gravando' e o índice único de
            // gravação ativa bloqueia toda nova gravação até o Tray ser reiniciado.
            reuniao.RegistrarFalha(exception.Message);
            await reuniaoRepository.SalvarAsync(reuniao, CancellationToken.None);
            throw;
        }

        await jobQueue.EnfileirarAsync(reuniao.Id, relogio.GetUtcNow(), cancellationToken);
        try
        {
            await workerLauncher.IniciarAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new WorkerNaoIniciadoException(
                $"A gravação foi salva, mas o Worker não iniciou: {exception.Message}",
                exception);
        }
    }
}

using Anamnesis.Application.Contracts;
using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.UseCases;

public sealed class ControlarGravacaoHandler(
    IReuniaoRepository reuniaoRepository,
    IJobQueue jobQueue,
    IGravador gravador,
    IWorkerLauncher workerLauncher,
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
            await gravador.IniciarAsync(cancellationToken);
            return reuniao.Id;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            reuniao.RegistrarFalha(exception.Message);
            await reuniaoRepository.SalvarAsync(reuniao, CancellationToken.None);
            throw;
        }
    }

    public async Task FinalizarAsync(Guid reuniaoId, CancellationToken cancellationToken)
    {
        var reuniao = await reuniaoRepository.ObterAsync(reuniaoId, cancellationToken)
            ?? throw new InvalidOperationException($"A reunião '{reuniaoId}' não foi encontrada.");
        var caminhoArquivo = await gravador.FinalizarAsync(cancellationToken);
        reuniao.FinalizarGravacao(caminhoArquivo, relogio.GetUtcNow());
        await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);
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

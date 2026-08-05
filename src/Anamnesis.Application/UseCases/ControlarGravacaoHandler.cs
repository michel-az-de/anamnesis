using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;

namespace Anamnesis.Application.UseCases;

public sealed class ControlarGravacaoHandler(
    IReuniaoRepository reuniaoRepository,
    IJobQueue jobQueue,
    IGravador gravador,
    IWorkerLauncher workerLauncher,
    IObsPreflight obsPreflight,
    TimeProvider relogio,
    JornalOperacional? journal = null)
{
    private readonly JornalOperacional _journal = journal ?? JornalOperacional.Nulo;

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
            await _journal.RegistrarAsync(
                NivelEventoOperacional.Info,
                CodigosEventoOperacional.GravacaoIniciada,
                "OBS",
                "Gravação iniciada.",
                reuniao.Id,
                null,
                new MetadadosEventoOperacional(Operacao: "iniciar_gravacao", Resultado: "sucesso"),
                cancellationToken);
            return reuniao.Id;
        }
        catch (Exception exception)
        {
            reuniao.RegistrarFalha(exception.Message);
            await reuniaoRepository.SalvarAsync(reuniao, CancellationToken.None);
            await _journal.RegistrarFalhaAsync(
                "OBS",
                "iniciar_gravacao",
                exception,
                reuniao.Id,
                null,
                CancellationToken.None);
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

        try
        {
            await obsPreflight.PrepararAsync(cancellationToken);
            if (await gravador.EstaGravandoAsync(cancellationToken))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            await _journal.RegistrarFalhaAsync(
                "OBS",
                "reconciliar_gravacao",
                exception,
                reuniao.Id,
                null,
                CancellationToken.None);
            throw;
        }

        reuniao.RegistrarFalha(
            "A gravação foi interrompida antes da confirmação do OBS e pode ser iniciada novamente.");
        await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);
        await _journal.RegistrarFalhaAsync(
            "OBS",
            "reconciliar_gravacao",
            new InvalidOperationException("Gravação não confirmada pelo OBS."),
            reuniao.Id,
            null,
            CancellationToken.None);
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
            await _journal.RegistrarAsync(
                NivelEventoOperacional.Info,
                CodigosEventoOperacional.GravacaoFinalizada,
                "OBS",
                "Gravação finalizada.",
                reuniao.Id,
                null,
                new MetadadosEventoOperacional(Operacao: "finalizar_gravacao", Resultado: "sucesso"),
                cancellationToken);
        }
        catch (Exception exception)
        {
            // Sem registrar a falha, a reunião fica presa em 'Gravando' e o índice único de
            // gravação ativa bloqueia toda nova gravação até o Tray ser reiniciado. A
            // compensação ignora o token cancelado e o throw preserva a exceção original.
            reuniao.RegistrarFalha(exception.Message);
            await reuniaoRepository.SalvarAsync(reuniao, CancellationToken.None);
            await _journal.RegistrarFalhaAsync(
                "OBS",
                "finalizar_gravacao",
                exception,
                reuniao.Id,
                null,
                CancellationToken.None);
            throw;
        }

        JobProcessamento job;
        try
        {
            job = await jobQueue.EnfileirarAsync(reuniao.Id, relogio.GetUtcNow(), cancellationToken);
        }
        catch (Exception exception)
        {
            await _journal.RegistrarFalhaAsync(
                "Fila",
                "enfileirar_job",
                exception,
                reuniao.Id,
                null,
                CancellationToken.None);
            throw;
        }

        await _journal.RegistrarAsync(
            NivelEventoOperacional.Info,
            CodigosEventoOperacional.JobEnfileirado,
            "Fila",
            "Processamento enfileirado.",
            reuniao.Id,
            job.Id,
            new MetadadosEventoOperacional(Operacao: "enfileirar_job", Resultado: "sucesso"),
            cancellationToken);
        try
        {
            await workerLauncher.IniciarAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _journal.RegistrarFalhaAsync(
                "Worker",
                "iniciar_worker",
                exception,
                reuniao.Id,
                job.Id,
                CancellationToken.None);
            throw new WorkerNaoIniciadoException(
                $"A gravação foi salva, mas o Worker não iniciou: {exception.Message}",
                exception);
        }
    }
}

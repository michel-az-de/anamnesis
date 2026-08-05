using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;

namespace Anamnesis.Application.UseCases;

public sealed class ProcessarReuniaoHandler(
    IReuniaoRepository reuniaoRepository,
    ITranscritor transcritor,
    IAtaRunner ataRunner,
    IArquivador arquivador,
    IArtefatoRepository artefatoRepository,
    TimeProvider relogio,
    JornalOperacional? journal = null)
{
    private readonly JornalOperacional _journal = journal ?? JornalOperacional.Nulo;

    public Task ExecutarAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
        ExecutarAsync(reuniaoId, null, cancellationToken);

    public async Task ExecutarAsync(Guid reuniaoId, Guid? jobId, CancellationToken cancellationToken)
    {
        var reuniao = await reuniaoRepository.ObterAsync(reuniaoId, cancellationToken)
            ?? throw new InvalidOperationException($"A reunião '{reuniaoId}' não foi encontrada.");
        var componenteFalha = "Processamento";
        var operacaoFalha = "processar_reuniao";

        try
        {
            if (reuniao.Status == StatusReuniao.Falha)
            {
                reuniao.ReiniciarProcessamento();
            }

            reuniao.IniciarTranscricao();
            await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);
            await _journal.RegistrarAsync(
                NivelEventoOperacional.Info,
                CodigosEventoOperacional.TranscricaoIniciada,
                "Whisper",
                "Transcrição iniciada.",
                reuniao.Id,
                jobId,
                new MetadadosEventoOperacional(Operacao: "transcrever", Resultado: "iniciado"),
                cancellationToken);

            componenteFalha = "Whisper";
            operacaoFalha = "transcrever";
            var inicioTranscricao = relogio.GetTimestamp();
            var transcricaoGerada = await transcritor.TranscreverAsync(
                reuniao.Gravacao!.CaminhoArquivo,
                cancellationToken);

            reuniao.RegistrarTranscricao(new Transcricao(
                transcricaoGerada.Texto,
                transcricaoGerada.Idioma,
                relogio.GetUtcNow()));
            await _journal.RegistrarAsync(
                NivelEventoOperacional.Info,
                CodigosEventoOperacional.TranscricaoConcluida,
                "Whisper",
                "Transcrição concluída.",
                reuniao.Id,
                jobId,
                new MetadadosEventoOperacional(
                    Operacao: "transcrever",
                    Resultado: "sucesso",
                    DuracaoMs: relogio.GetElapsedTime(inicioTranscricao).TotalMilliseconds),
                cancellationToken);

            componenteFalha = "Ata";
            operacaoFalha = "gerar_ata";
            var ataGerada = await ataRunner.GerarAsync(reuniao, transcricaoGerada, cancellationToken);

            reuniao.RegistrarAta(new Ata(
                ataGerada.ResumoExecutivo,
                ataGerada.Decisoes,
                ataGerada.Tarefas,
                relogio.GetUtcNow()));
            await _journal.RegistrarAsync(
                NivelEventoOperacional.Info,
                CodigosEventoOperacional.AtaGerada,
                "Ata",
                "Ata estruturada gerada.",
                reuniao.Id,
                jobId,
                new MetadadosEventoOperacional(Operacao: "gerar_ata", Resultado: "sucesso"),
                cancellationToken);

            componenteFalha = "Arquivo";
            operacaoFalha = "arquivar_reuniao";
            var artefatos = await arquivador.ArquivarAsync(reuniao, cancellationToken);
            await artefatoRepository.SalvarAsync(artefatos, cancellationToken);
            reuniao.MarcarArquivada(relogio.GetUtcNow());
            await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);
            await _journal.RegistrarAsync(
                NivelEventoOperacional.Info,
                CodigosEventoOperacional.ReuniaoArquivada,
                "Arquivo",
                "Reunião arquivada.",
                reuniao.Id,
                jobId,
                new MetadadosEventoOperacional(Operacao: "arquivar_reuniao", Resultado: "sucesso"),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            reuniao.RegistrarFalha(exception.Message);
            await reuniaoRepository.SalvarAsync(reuniao, CancellationToken.None);
            await _journal.RegistrarFalhaAsync(
                componenteFalha,
                operacaoFalha,
                exception,
                reuniao.Id,
                jobId,
                CancellationToken.None);
            throw;
        }
    }
}

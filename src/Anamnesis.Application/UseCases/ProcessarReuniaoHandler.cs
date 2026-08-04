using Anamnesis.Application.Contracts;
using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.UseCases;

public sealed class ProcessarReuniaoHandler(
    IReuniaoRepository reuniaoRepository,
    ITranscritor transcritor,
    IAtaRunner ataRunner,
    IArquivador arquivador,
    TimeProvider relogio)
{
    public async Task ExecutarAsync(Guid reuniaoId, CancellationToken cancellationToken)
    {
        var reuniao = await reuniaoRepository.ObterAsync(reuniaoId, cancellationToken)
            ?? throw new InvalidOperationException($"A reunião '{reuniaoId}' não foi encontrada.");

        try
        {
            reuniao.IniciarTranscricao();
            await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);

            var transcricaoGerada = await transcritor.TranscreverAsync(
                reuniao.Gravacao!.CaminhoArquivo,
                cancellationToken);

            reuniao.RegistrarTranscricao(new Transcricao(
                transcricaoGerada.Texto,
                transcricaoGerada.Idioma,
                relogio.GetUtcNow()));

            var ataGerada = await ataRunner.GerarAsync(reuniao, transcricaoGerada, cancellationToken);

            reuniao.RegistrarAta(new Ata(
                ataGerada.ResumoExecutivo,
                ataGerada.Decisoes,
                ataGerada.Tarefas,
                relogio.GetUtcNow()));

            await arquivador.ArquivarAsync(reuniao, cancellationToken);
            reuniao.MarcarArquivada();
            await reuniaoRepository.SalvarAsync(reuniao, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            reuniao.RegistrarFalha(exception.Message);
            await reuniaoRepository.SalvarAsync(reuniao, CancellationToken.None);
            throw;
        }
    }
}

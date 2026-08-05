using Anamnesis.Application.UseCases;
using Anamnesis.Infrastructure.Arquivos;
using Anamnesis.Infrastructure.Cli;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Persistencia;
using Anamnesis.Infrastructure.Retencao;
using Anamnesis.Infrastructure.Whisper;

namespace Anamnesis.Worker;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var modoRetencao = ModoRetencaoWorkerOptions.Interpretar(args);
            var caminhoConfiguracao = ObterCaminhoConfiguracao();
            await Console.Out.WriteLineAsync($"Worker iniciado. Configuração: {caminhoConfiguracao}");
            var configuracao = await new ArquivoConfiguracao(caminhoConfiguracao)
                .CarregarAsync(CancellationToken.None);
            var reuniaoRepository = new SqliteReuniaoRepository(configuracao.CaminhoBanco);
            if (modoRetencao is not null)
            {
                return await ExecutarRetencaoAsync(modoRetencao, reuniaoRepository);
            }

            var fila = new SqliteJobQueue(configuracao.CaminhoBanco);
            var processarReuniao = new ProcessarReuniaoHandler(
                reuniaoRepository,
                new WhisperTranscritor(new WhisperOptions(
                    configuracao.CaminhoExecutavelWhisper,
                    configuracao.CaminhoModeloWhisper,
                    configuracao.IdiomaWhisper,
                    configuracao.CaminhoExecutavelFfmpeg,
                    configuracao.ImagemDockerWhisper)),
                new CliAtaRunner(new CliAtaRunnerOptions(
                    configuracao.NomeCli,
                    configuracao.CaminhoExecutavelCli,
                    configuracao.ArgumentosCli)),
                new DiscoArquivador(configuracao.DiretorioArquivo),
                TimeProvider.System);
            var consumer = new ReuniaoConsumer(fila, processarReuniao, TimeProvider.System);

            await consumer.RetomarAsync(CancellationToken.None);
            var jobsProcessados = 0;
            while (await consumer.ProcessarProximoAsync(CancellationToken.None))
            {
                jobsProcessados++;
                await Console.Out.WriteLineAsync($"Job processado. Total: {jobsProcessados}");
            }

            await Console.Out.WriteLineAsync("Fila vazia. Worker finalizado com sucesso.");
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"Falha do Worker: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> ExecutarRetencaoAsync(
        ModoRetencaoWorkerOptions options,
        SqliteReuniaoRepository reuniaoRepository)
    {
        var retencao = new RetencaoGravacaoHandler(
            reuniaoRepository,
            new LixeiraWindows(),
            new RelogioFixo(options.Agora));
        var resultado = await retencao.SimularAsync(options.ReuniaoId, CancellationToken.None);
        await Console.Out.WriteLineAsync(
            $"Retenção avaliada. Reunião: {options.ReuniaoId:N}. Elegível: {resultado.PodeMover}. " +
            $"Caminho: {resultado.CaminhoArquivo ?? "não informado"}. Motivo: {resultado.Motivo ?? "nenhum"}.");
        if (!options.Aplicar)
        {
            await Console.Out.WriteLineAsync("Simulação concluída sem alterar arquivo ou estado.");
            return 0;
        }

        await retencao.AplicarAsync(options.ReuniaoId, CancellationToken.None);
        await Console.Out.WriteLineAsync("Retenção aplicada. Gravação movida para a Lixeira e reunião marcada como Excluida.");
        return 0;
    }

    private static string ObterCaminhoConfiguracao()
    {
        var caminhoDefinido = Environment.GetEnvironmentVariable("ANAMNESIS_CONFIGURACAO");
        if (!string.IsNullOrWhiteSpace(caminhoDefinido))
        {
            return Path.GetFullPath(caminhoDefinido);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Anamnesis",
            "config.json");
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}

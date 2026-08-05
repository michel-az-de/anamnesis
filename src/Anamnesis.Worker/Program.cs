using System.Text;
using Anamnesis.Application.UseCases;
using Anamnesis.Infrastructure.Arquivos;
using Anamnesis.Infrastructure.Cli;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Persistencia;
using Anamnesis.Infrastructure.Processos;
using Anamnesis.Infrastructure.Retencao;
using Anamnesis.Infrastructure.Whisper;

namespace Anamnesis.Worker;

internal static class Program
{
    /// <summary>
    /// Janela extra de leitura da fila antes de encerrar. O Tray enfileira o job e só depois
    /// lança o Worker; se este processo saísse imediatamente, um Worker recém-lançado que não
    /// obteve a exclusividade desistiria e o job ficaria parado até a próxima abertura do Tray.
    /// </summary>
    private static readonly TimeSpan CarenciaFinal = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Síncrono de propósito: o mutex de instância única precisa ser liberado pela mesma thread
    /// que o adquiriu, o que não é garantido na continuação de um <c>async Main</c>.
    /// </summary>
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        try
        {
            var modoRetencao = ModoRetencaoWorkerOptions.Interpretar(args);
            var caminhoConfiguracao = ObterCaminhoConfiguracao();
            Console.WriteLine($"Worker iniciado. Configuração: {caminhoConfiguracao}");
            var configuracao = new ArquivoConfiguracao(caminhoConfiguracao)
                .CarregarAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var reuniaoRepository = new SqliteReuniaoRepository(configuracao.CaminhoBanco);
            if (modoRetencao is not null)
            {
                // Retenção é um comando pontual que não toca na fila: exigir exclusividade aqui
                // faria o comando virar um no-op silencioso durante um processamento longo.
                return ExecutarRetencaoAsync(modoRetencao, reuniaoRepository)
                    .GetAwaiter()
                    .GetResult();
            }

            using var instanciaUnica = InstanciaUnicaWorker.TentarAdquirir(configuracao.CaminhoBanco);
            if (instanciaUnica is null)
            {
                Console.WriteLine("Outro Worker já está processando esta fila. Encerrando sem processar.");
                return 0;
            }

            return ConsumirFilaAsync(configuracao, reuniaoRepository).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Falha do Worker: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> ConsumirFilaAsync(
        ConfiguracaoAnamnesis configuracao,
        SqliteReuniaoRepository reuniaoRepository)
    {
        var fila = new SqliteJobQueue(configuracao.CaminhoBanco);
        var whisperOptions = new WhisperOptions(
            configuracao.CaminhoExecutavelWhisper,
            configuracao.CaminhoModeloWhisper,
            configuracao.IdiomaWhisper,
            configuracao.CaminhoExecutavelFfmpeg,
            configuracao.ImagemDockerWhisper);
        IDockerPreflight? dockerPreflight = string.IsNullOrWhiteSpace(configuracao.ImagemDockerWhisper)
            ? null
            : new DockerProcessPreflight(
                configuracao.CaminhoExecutavelWhisper,
                DockerProcessPreflight.ResolverCaminhoExecutavel(configuracao.CaminhoExecutavelDockerDesktop));
        var processarReuniao = new ProcessarReuniaoHandler(
            reuniaoRepository,
            new WhisperTranscritor(whisperOptions, dockerPreflight),
            new CliAtaRunner(new CliAtaRunnerOptions(
                configuracao.NomeCli,
                configuracao.CaminhoExecutavelCli,
                configuracao.ArgumentosCli)),
            new DiscoArquivador(configuracao.DiretorioArquivo),
            new SqliteArtefatoRepository(configuracao.CaminhoBanco),
            TimeProvider.System);
        var consumer = new ReuniaoConsumer(fila, processarReuniao, TimeProvider.System);
        var jobsProcessados = 0;

        async Task DrenarAsync()
        {
            while (await consumer.ProcessarProximoAsync(CancellationToken.None))
            {
                jobsProcessados++;
                await Console.Out.WriteLineAsync($"Job processado. Total: {jobsProcessados}");
            }
        }

        await consumer.RetomarAsync(CancellationToken.None);
        await DrenarAsync();
        await Task.Delay(CarenciaFinal);
        await DrenarAsync();

        await Console.Out.WriteLineAsync("Fila vazia. Worker finalizado com sucesso.");
        return 0;
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
            $"Caminho: {resultado.CaminhoArquivo ?? "não informado"}. Motivo: {resultado.Descricao ?? "nenhum"}.");
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

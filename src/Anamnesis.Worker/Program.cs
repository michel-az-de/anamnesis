using System.Text;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Application.UseCases;
using Anamnesis.Infrastructure.Arquivos;
using Anamnesis.Infrastructure.Cli;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Observabilidade;
using Anamnesis.Infrastructure.Persistencia;
using Anamnesis.Infrastructure.Processos;
using Anamnesis.Infrastructure.Retencao;
using Anamnesis.Infrastructure.Whisper;

namespace Anamnesis.Worker;

internal static class Program
{
    /// <summary>
    /// Síncrono de propósito: o mutex de instância única precisa ser liberado pela mesma thread
    /// que o adquiriu, o que não é garantido na continuação de um <c>async Main</c>.
    /// </summary>
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        try
        {
            var caminhoConfiguracao = ObterCaminhoConfiguracao();
            return Executar(
                args,
                caminhoConfiguracao,
                InstanciaUnicaWorker.AdquirirAguardando,
                Console.WriteLine);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Falha do Worker: {exception.Message}");
            return 1;
        }
    }

    internal static int Executar(
        string[] args,
        string caminhoConfiguracao,
        Func<string, InstanciaUnicaWorker?> adquirirExclusividade,
        Action<string> escrever)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoConfiguracao);
        ArgumentNullException.ThrowIfNull(adquirirExclusividade);
        ArgumentNullException.ThrowIfNull(escrever);

        var modoRetencao = ModoRetencaoWorkerOptions.Interpretar(args);
        escrever($"Worker iniciado. Configuração: {caminhoConfiguracao}");
        var configuracao = new ArquivoConfiguracao(caminhoConfiguracao)
            .CarregarAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var reuniaoRepository = new SqliteReuniaoRepository(configuracao.CaminhoBanco);
        var eventoRepository = new SqliteEventoOperacionalRepository(configuracao.CaminhoBanco);
        var journal = new JornalOperacional(eventoRepository, TimeProvider.System);
        if (modoRetencao is not null)
        {
            // Retenção é um comando pontual que não toca na fila: exigir exclusividade aqui
            // faria o comando virar um no-op silencioso durante um processamento longo.
            return ExecutarRetencaoAsync(modoRetencao, reuniaoRepository, journal)
                .GetAwaiter()
                .GetResult();
        }

        using var instanciaUnica = adquirirExclusividade(configuracao.CaminhoBanco);
        if (instanciaUnica is null)
        {
            journal.RegistrarAsync(
                    NivelEventoOperacional.Info,
                    CodigosEventoOperacional.WorkerExclusividadeExpirada,
                    "Worker",
                    "A espera pela exclusividade atingiu o limite; a fila foi preservada.",
                    null,
                    null,
                    new MetadadosEventoOperacional(
                        Operacao: "aguardar_exclusividade",
                        Resultado: "limite",
                        DuracaoMs: InstanciaUnicaWorker.LimitePadraoDeEspera.TotalMilliseconds),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            escrever(
                "Outro Worker reteve a exclusividade além do limite. Encerrando sem processar; a fila permanece intacta.");
            return 0;
        }

        if (instanciaUnica.AguardouOutraInstancia)
        {
            escrever(
                "Outro Worker estava processando esta fila. Exclusividade transferida; conferindo pendências.");
        }

        return ConsumirFilaAsync(configuracao, reuniaoRepository, journal).GetAwaiter().GetResult();
    }

    private static async Task<int> ConsumirFilaAsync(
        ConfiguracaoAnamnesis configuracao,
        SqliteReuniaoRepository reuniaoRepository,
        JornalOperacional journal)
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
                configuracao.ArgumentosCli,
                ArgumentoArquivoSaida: configuracao.ResolverArgumentoArquivoSaidaCli())),
            new DiscoArquivador(configuracao.DiretorioArquivo),
            new SqliteArtefatoRepository(configuracao.CaminhoBanco),
            TimeProvider.System,
            journal);
        var consumer = new ReuniaoConsumer(fila, processarReuniao, TimeProvider.System, journal);
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

        await Console.Out.WriteLineAsync("Fila vazia. Worker finalizado com sucesso.");
        return 0;
    }

    private static async Task<int> ExecutarRetencaoAsync(
        ModoRetencaoWorkerOptions options,
        SqliteReuniaoRepository reuniaoRepository,
        JornalOperacional journal)
    {
        var retencao = new RetencaoGravacaoHandler(
            reuniaoRepository,
            new LixeiraWindows(),
            new RelogioFixo(options.Agora),
            journal);
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

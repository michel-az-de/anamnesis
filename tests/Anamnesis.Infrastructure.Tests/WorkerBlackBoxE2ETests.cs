using System.Diagnostics;
using System.Globalization;
using System.Text;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Persistencia;
using Anamnesis.Infrastructure.Processos;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WorkerBlackBoxE2ETests : IAsyncLifetime
{
    private static readonly TimeSpan LimiteExecucaoWorker = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LimiteEncerramentoForcado = TimeSpan.FromSeconds(5);
    private string _diretorio = null!;
    private bool _preservarEvidencias;

    public Task InitializeAsync()
    {
        var diretorioEvidencias = Environment.GetEnvironmentVariable("ANAMNESIS_WORKER_E2E_EVIDENCIAS");
        _preservarEvidencias = !string.IsNullOrWhiteSpace(diretorioEvidencias);
        _diretorio = _preservarEvidencias
            ? Path.GetFullPath(diretorioEvidencias!)
            : Path.Combine(Path.GetTempPath(), $"anamnesis-worker-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_diretorio);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (!_preservarEvidencias && Directory.Exists(_diretorio))
        {
            Directory.Delete(_diretorio, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task DeveExecutarWorkerEmProcessoSeparadoComConfiguracaoIsolada()
    {
        var caminhoBanco = Path.Combine(_diretorio, "anamnesis.db");
        var caminhoGravacao = Path.Combine(_diretorio, "gravacao.mkv");
        var caminhoModelo = Path.Combine(_diretorio, "modelo.bin");
        await File.WriteAllTextAsync(caminhoGravacao, "gravação black box");
        await File.WriteAllTextAsync(caminhoModelo, "modelo de teste");
        var caminhoFfmpeg = await CriarFfmpegFakeAsync();
        var caminhoWhisper = await CriarWhisperFakeAsync();
        var (caminhoCli, caminhoEntradaCli) = await CriarCliFakeAsync();
        var caminhoConfiguracao = Path.Combine(_diretorio, "config.json");
        await new ArquivoConfiguracao(caminhoConfiguracao).SalvarAsync(
            new ConfiguracaoAnamnesis
            {
                CaminhoBanco = caminhoBanco,
                DiretorioArquivo = Path.Combine(_diretorio, "arquivo"),
                CaminhoExecutavelFfmpeg = caminhoFfmpeg,
                CaminhoExecutavelWhisper = caminhoWhisper,
                CaminhoModeloWhisper = caminhoModelo,
                CaminhoExecutavelCli = caminhoCli,
                NomeCli = "CLI black box"
            },
            CancellationToken.None);

        var agora = DateTimeOffset.UtcNow;
        var reuniao = new Reuniao(Guid.NewGuid(), "Worker black box", agora);
        reuniao.IniciarGravacao(agora);
        reuniao.FinalizarGravacao(caminhoGravacao, agora);
        var repository = new SqliteReuniaoRepository(caminhoBanco);
        var fila = new SqliteJobQueue(caminhoBanco);
        await repository.SalvarAsync(reuniao, CancellationToken.None);
        await fila.EnfileirarAsync(reuniao.Id, agora, CancellationToken.None);

        var resultado = await ExecutarWorkerAsync(caminhoConfiguracao);
        await File.WriteAllTextAsync(Path.Combine(_diretorio, "worker.stdout.log"), resultado.Stdout);
        await File.WriteAllTextAsync(Path.Combine(_diretorio, "worker.stderr.log"), resultado.Stderr);

        Assert.Equal(0, resultado.CodigoSaida);
        Assert.Contains("Worker iniciado", resultado.Stdout, StringComparison.Ordinal);
        Assert.Contains("Job processado", resultado.Stdout, StringComparison.Ordinal);
        Assert.Contains("Fila vazia", resultado.Stdout, StringComparison.Ordinal);

        var arquivada = await repository.ObterAsync(reuniao.Id, CancellationToken.None);
        Assert.NotNull(arquivada);
        Assert.Equal(StatusReuniao.Arquivada, arquivada!.Status);
        Assert.Contains("transcricao", await File.ReadAllTextAsync(caminhoEntradaCli), StringComparison.OrdinalIgnoreCase);
        var diretorioArquivado = Path.Combine(
            _diretorio,
            "arquivo",
            agora.UtcDateTime.Year.ToString(CultureInfo.InvariantCulture),
            agora.UtcDateTime.Month.ToString("00", CultureInfo.InvariantCulture),
            reuniao.Id.ToString("N"));
        Assert.True(File.Exists(Path.Combine(diretorioArquivado, "ata.md")));
        Assert.True(File.Exists(Path.Combine(diretorioArquivado, "transcricao.md")));
        await CriarResultadoAsync(reuniao.Id, arquivada.Status, resultado.CodigoSaida, caminhoConfiguracao, caminhoBanco, diretorioArquivado, caminhoEntradaCli);
        Assert.True(File.Exists(Path.Combine(_diretorio, "resultado.md")));
    }

    [Fact]
    public async Task DeveProcessarUmaUnicaVezQuandoDoisWorkersIniciamJuntos()
    {
        var caminhoBanco = Path.Combine(_diretorio, "concorrencia.db");
        var caminhoGravacao = Path.Combine(_diretorio, "gravacao-concorrencia.mkv");
        var caminhoModelo = Path.Combine(_diretorio, "modelo-concorrencia.bin");
        var caminhoContador = Path.Combine(_diretorio, "whisper-invocacoes.log");
        await File.WriteAllTextAsync(caminhoGravacao, "gravação concorrente");
        await File.WriteAllTextAsync(caminhoModelo, "modelo de teste");
        var caminhoFfmpeg = await CriarFfmpegFakeAsync();
        var caminhoWhisper = await CriarWhisperFakeLentoAsync(caminhoContador);
        var (caminhoCli, _) = await CriarCliFakeAsync();
        var caminhoConfiguracao = Path.Combine(_diretorio, "config-concorrencia.json");
        await new ArquivoConfiguracao(caminhoConfiguracao).SalvarAsync(
            new ConfiguracaoAnamnesis
            {
                CaminhoBanco = caminhoBanco,
                DiretorioArquivo = Path.Combine(_diretorio, "arquivo-concorrencia"),
                CaminhoExecutavelFfmpeg = caminhoFfmpeg,
                CaminhoExecutavelWhisper = caminhoWhisper,
                CaminhoModeloWhisper = caminhoModelo,
                CaminhoExecutavelCli = caminhoCli,
                NomeCli = "CLI concorrência"
            },
            CancellationToken.None);

        var agora = DateTimeOffset.UtcNow;
        var reuniao = new Reuniao(Guid.NewGuid(), "Worker concorrente", agora);
        reuniao.IniciarGravacao(agora);
        reuniao.FinalizarGravacao(caminhoGravacao, agora);
        var repository = new SqliteReuniaoRepository(caminhoBanco);
        await repository.SalvarAsync(reuniao, CancellationToken.None);
        await new SqliteJobQueue(caminhoBanco).EnfileirarAsync(reuniao.Id, agora, CancellationToken.None);

        // O primeiro Worker fica preso no Whisper lento; o segundo sobe no meio disso.
        var primeiro = ExecutarWorkerAsync(caminhoConfiguracao);
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var segundo = ExecutarWorkerAsync(caminhoConfiguracao);
        var resultados = await Task.WhenAll(primeiro, segundo);

        await File.WriteAllTextAsync(
            Path.Combine(_diretorio, "concorrencia.stdout.log"),
            string.Join(Environment.NewLine + "---" + Environment.NewLine, resultados.Select(r => r.Stdout)));

        Assert.All(resultados, resultado => Assert.Equal(0, resultado.CodigoSaida));
        Assert.Equal(1, resultados.Count(resultado =>
            resultado.Stdout.Contains("Job processado", StringComparison.Ordinal)));
        Assert.Equal(1, resultados.Count(resultado =>
            resultado.Stdout.Contains("Exclusividade transferida", StringComparison.Ordinal)));

        var invocacoesWhisper = (await File.ReadAllLinesAsync(caminhoContador))
            .Count(linha => !string.IsNullOrWhiteSpace(linha));
        Assert.Equal(1, invocacoesWhisper);

        var arquivada = await repository.ObterAsync(reuniao.Id, CancellationToken.None);
        Assert.NotNull(arquivada);
        Assert.Equal(StatusReuniao.Arquivada, arquivada!.Status);
        Assert.Null(arquivada.MotivoFalha);
    }

    [Fact]
    public async Task DeveProcessarJobEnfileiradoDuranteTransferenciaDeExclusividade()
    {
        var caminhoBanco = Path.Combine(_diretorio, "handoff.db");
        var caminhoGravacao = Path.Combine(_diretorio, "gravacao-handoff.mkv");
        var caminhoModelo = Path.Combine(_diretorio, "modelo-handoff.bin");
        await File.WriteAllTextAsync(caminhoGravacao, "gravação durante handoff");
        await File.WriteAllTextAsync(caminhoModelo, "modelo de teste");
        var caminhoFfmpeg = await CriarFfmpegFakeAsync();
        var caminhoWhisper = await CriarWhisperFakeAsync();
        var (caminhoCli, _) = await CriarCliFakeAsync();
        var caminhoConfiguracao = Path.Combine(_diretorio, "config-handoff.json");
        await new ArquivoConfiguracao(caminhoConfiguracao).SalvarAsync(
            new ConfiguracaoAnamnesis
            {
                CaminhoBanco = caminhoBanco,
                DiretorioArquivo = Path.Combine(_diretorio, "arquivo-handoff"),
                CaminhoExecutavelFfmpeg = caminhoFfmpeg,
                CaminhoExecutavelWhisper = caminhoWhisper,
                CaminhoModeloWhisper = caminhoModelo,
                CaminhoExecutavelCli = caminhoCli,
                NomeCli = "CLI handoff"
            },
            CancellationToken.None);

        var agora = DateTimeOffset.UtcNow;
        var reuniao = new Reuniao(Guid.NewGuid(), "Worker handoff", agora);
        reuniao.IniciarGravacao(agora);
        reuniao.FinalizarGravacao(caminhoGravacao, agora);
        var repository = new SqliteReuniaoRepository(caminhoBanco);
        await repository.SalvarAsync(reuniao, CancellationToken.None);
        await new SqliteJobQueue(caminhoBanco).EnfileirarAsync(reuniao.Id, agora, CancellationToken.None);

        using var exclusividadeAdquirida = new ManualResetEventSlim();
        using var liberarExclusividade = new ManualResetEventSlim();
        Exception? falhaNoDonoAnterior = null;
        var donoAnterior = new Thread(() =>
        {
            try
            {
                using var exclusividade = InstanciaUnicaWorker.TentarAdquirir(caminhoBanco);
                if (exclusividade is null)
                {
                    throw new InvalidOperationException("O dono anterior não adquiriu a exclusividade do teste.");
                }

                exclusividadeAdquirida.Set();
                liberarExclusividade.Wait();
            }
            catch (Exception exception)
            {
                falhaNoDonoAnterior = exception;
                exclusividadeAdquirida.Set();
            }
        });
        donoAnterior.Start();
        exclusividadeAdquirida.Wait();
        Assert.Null(falhaNoDonoAnterior);

        var worker = ExecutarWorkerAsync(caminhoConfiguracao);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.False(worker.IsCompleted);
        }
        finally
        {
            liberarExclusividade.Set();
            donoAnterior.Join();
        }

        var resultado = await worker;
        Assert.Equal(0, resultado.CodigoSaida);
        Assert.Contains("Exclusividade transferida", resultado.Stdout, StringComparison.Ordinal);
        Assert.Contains("Job processado", resultado.Stdout, StringComparison.Ordinal);
        Assert.Equal(StatusReuniao.Arquivada, (await repository.ObterAsync(reuniao.Id, CancellationToken.None))!.Status);
    }

    [Fact]
    public async Task DeveEncerrarArvoreDoWorkerQuandoExcedeLimite()
    {
        var caminhoBanco = Path.Combine(_diretorio, "timeout.db");
        var caminhoGravacao = Path.Combine(_diretorio, "gravacao-timeout.mkv");
        var caminhoModelo = Path.Combine(_diretorio, "modelo-timeout.bin");
        var caminhoMarcador = Path.Combine(_diretorio, "whisper-timeout-iniciado.txt");
        await File.WriteAllTextAsync(caminhoGravacao, "gravação para timeout");
        await File.WriteAllTextAsync(caminhoModelo, "modelo de teste");
        var caminhoFfmpeg = await CriarFfmpegFakeAsync();
        var caminhoWhisper = await CriarWhisperFakeTravadoAsync(caminhoMarcador);
        var (caminhoCli, _) = await CriarCliFakeAsync();
        var caminhoConfiguracao = Path.Combine(_diretorio, "config-timeout.json");
        await new ArquivoConfiguracao(caminhoConfiguracao).SalvarAsync(
            new ConfiguracaoAnamnesis
            {
                CaminhoBanco = caminhoBanco,
                DiretorioArquivo = Path.Combine(_diretorio, "arquivo-timeout"),
                CaminhoExecutavelFfmpeg = caminhoFfmpeg,
                CaminhoExecutavelWhisper = caminhoWhisper,
                CaminhoModeloWhisper = caminhoModelo,
                CaminhoExecutavelCli = caminhoCli,
                NomeCli = "CLI timeout"
            },
            CancellationToken.None);

        var agora = DateTimeOffset.UtcNow;
        var reuniao = new Reuniao(Guid.NewGuid(), "Worker timeout", agora);
        reuniao.IniciarGravacao(agora);
        reuniao.FinalizarGravacao(caminhoGravacao, agora);
        await new SqliteReuniaoRepository(caminhoBanco).SalvarAsync(reuniao, CancellationToken.None);
        await new SqliteJobQueue(caminhoBanco).EnfileirarAsync(reuniao.Id, agora, CancellationToken.None);

        var excecao = await Assert.ThrowsAsync<TimeoutException>(() =>
            ExecutarWorkerAsync(caminhoConfiguracao, limiteExecucao: TimeSpan.FromSeconds(5)));

        Assert.Contains("excedeu o limite", excecao.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(caminhoMarcador));
        using var instanciaDepoisDoKill = InstanciaUnicaWorker.TentarAdquirir(caminhoBanco);
        Assert.NotNull(instanciaDepoisDoKill);
    }

    [Fact]
    public async Task DeveSimularRetencaoEmProcessoSeparadoSemMoverArquivo()
    {
        var caminhoBanco = Path.Combine(_diretorio, "retencao.db");
        var caminhoGravacao = Path.Combine(_diretorio, "gravacao-retencao.mp4");
        await File.WriteAllTextAsync(caminhoGravacao, "gravação para retenção");
        var caminhoConfiguracao = Path.Combine(_diretorio, "config-retencao.json");
        await new ArquivoConfiguracao(caminhoConfiguracao).SalvarAsync(
            new ConfiguracaoAnamnesis
            {
                CaminhoBanco = caminhoBanco,
                DiretorioArquivo = Path.Combine(_diretorio, "arquivo")
            },
            CancellationToken.None);
        var arquivadaEm = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        var reuniao = new Reuniao(Guid.NewGuid(), "Retenção black box", arquivadaEm.AddHours(-1));
        reuniao.IniciarGravacao(arquivadaEm.AddMinutes(-50));
        reuniao.FinalizarGravacao(caminhoGravacao, arquivadaEm.AddMinutes(-40));
        reuniao.IniciarTranscricao();
        reuniao.RegistrarTranscricao(new Transcricao("Texto.", "pt", arquivadaEm.AddMinutes(-30)));
        reuniao.RegistrarAta(new Ata("Resumo.", [], [], arquivadaEm.AddMinutes(-20)));
        reuniao.MarcarArquivada(arquivadaEm);
        var repository = new SqliteReuniaoRepository(caminhoBanco);
        await repository.SalvarAsync(reuniao, CancellationToken.None);

        var resultado = await ExecutarWorkerAsync(caminhoConfiguracao, [
            "--retencao-simular",
            "--reuniao", reuniao.Id.ToString(),
            "--agora", "2026-09-04T12:00:00Z"
        ]);

        Assert.Equal(0, resultado.CodigoSaida);
        Assert.Contains("Elegível: True", resultado.Stdout, StringComparison.Ordinal);
        Assert.Contains("Simulação concluída", resultado.Stdout, StringComparison.Ordinal);
        Assert.True(File.Exists(caminhoGravacao));
        var preservada = await repository.ObterAsync(reuniao.Id, CancellationToken.None);
        Assert.Equal(StatusReuniao.Arquivada, preservada!.Status);
    }

    private static async Task<(int CodigoSaida, string Stdout, string Stderr)> ExecutarWorkerAsync(
        string caminhoConfiguracao,
        IReadOnlyList<string>? argumentos = null,
        TimeSpan? limiteExecucao = null)
    {
        using var processo = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
                WorkingDirectory = EncontrarRaizRepositorio(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            }
        };
        processo.StartInfo.ArgumentList.Add(Path.Combine(
            EncontrarRaizRepositorio(),
            "src",
            "Anamnesis.Worker",
            "bin",
            "Release",
            "net10.0-windows",
            "Anamnesis.Worker.dll"));
        foreach (var argumento in argumentos ?? [])
        {
            processo.StartInfo.ArgumentList.Add(argumento);
        }

        processo.StartInfo.Environment["ANAMNESIS_CONFIGURACAO"] = caminhoConfiguracao;
        processo.Start();
        var stdout = processo.StandardOutput.ReadToEndAsync();
        var stderr = processo.StandardError.ReadToEndAsync();
        using var limite = new CancellationTokenSource(limiteExecucao ?? LimiteExecucaoWorker);
        try
        {
            await processo.WaitForExitAsync(limite.Token);
        }
        catch (OperationCanceledException) when (limite.IsCancellationRequested)
        {
            if (!processo.HasExited)
            {
                try
                {
                    processo.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // O processo encerrou entre HasExited e Kill; a condicao desejada ja foi atingida.
                }
            }

            await processo.WaitForExitAsync().WaitAsync(LimiteEncerramentoForcado);
            var stdoutParcial = await stdout.WaitAsync(LimiteEncerramentoForcado);
            var stderrParcial = await stderr.WaitAsync(LimiteEncerramentoForcado);
            throw new TimeoutException(
                $"Worker excedeu o limite de {(limiteExecucao ?? LimiteExecucaoWorker).TotalSeconds:N0}s; "
                + $"a árvore de processos foi encerrada. stdout: {stdoutParcial} stderr: {stderrParcial}");
        }

        return (processo.ExitCode, await stdout, await stderr);
    }

    private static string EncontrarRaizRepositorio()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "Anamnesis.sln")))
        {
            diretorio = diretorio.Parent;
        }

        return diretorio?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }

    private async Task<string> CriarWhisperFakeAsync()
    {
        var caminho = Path.Combine(_diretorio, "whisper-fake.cmd");
        await File.WriteAllTextAsync(caminho, "@echo off\r\nset \"saida=\"\r\n:argumento\r\nif \"%~1\"==\"\" goto fim\r\nif \"%~1\"==\"-of\" (set \"saida=%~2\" & shift)\r\nshift\r\ngoto argumento\r\n:fim\r\necho Transcrição black box> \"%saida%.txt\"\r\n");
        return caminho;
    }

    /// <summary>
    /// Além de gerar a transcrição, conta as invocações e demora o bastante para que o
    /// segundo Worker suba enquanto o primeiro ainda está processando. O atraso usa `ping`
    /// porque `timeout` falha quando a entrada padrão do processo está redirecionada.
    /// </summary>
    private async Task<string> CriarWhisperFakeLentoAsync(string caminhoContador)
    {
        var caminho = Path.Combine(_diretorio, "whisper-fake-lento.cmd");
        await File.WriteAllTextAsync(
            caminho,
            "@echo off\r\nset \"saida=\"\r\n:argumento\r\nif \"%~1\"==\"\" goto fim\r\nif \"%~1\"==\"-of\" (set \"saida=%~2\" & shift)\r\nshift\r\ngoto argumento\r\n:fim\r\n"
                + $"echo invocacao>> \"{caminhoContador}\"\r\n"
                + "ping -n 4 127.0.0.1 > nul\r\n"
                + "echo Transcrição concorrente> \"%saida%.txt\"\r\n");
        return caminho;
    }

    private async Task<string> CriarWhisperFakeTravadoAsync(string caminhoMarcador)
    {
        var caminho = Path.Combine(_diretorio, "whisper-fake-travado.cmd");
        await File.WriteAllTextAsync(
            caminho,
            "@echo off\r\n"
                + $"echo iniciado> \"{caminhoMarcador}\"\r\n"
                + "ping -n 60 127.0.0.1 > nul\r\n");
        return caminho;
    }

    private async Task<string> CriarFfmpegFakeAsync()
    {
        var caminho = Path.Combine(_diretorio, "ffmpeg-fake.cmd");
        await File.WriteAllTextAsync(caminho, "@echo off\r\nset \"saida=\"\r\n:argumento\r\nif \"%~1\"==\"\" goto fim\r\nset \"saida=%~1\"\r\nshift\r\ngoto argumento\r\n:fim\r\necho WAV black box> \"%saida%\"\r\n");
        return caminho;
    }

    private async Task<(string CaminhoCli, string CaminhoEntrada)> CriarCliFakeAsync()
    {
        var caminhoEntrada = Path.Combine(_diretorio, "entrada-cli.json");
        var caminho = Path.Combine(_diretorio, "cli-fake.cmd");
        await File.WriteAllTextAsync(
            caminho,
            $"@echo off\r\nmore > \"{caminhoEntrada}\"\r\necho {{\"resumoExecutivo\":\"Resumo black box.\",\"decisoes\":[],\"tarefas\":[]}}\r\n");
        return (caminho, caminhoEntrada);
    }

    private Task CriarResultadoAsync(
        Guid reuniaoId,
        StatusReuniao statusFinal,
        int codigoSaida,
        string caminhoConfiguracao,
        string caminhoBanco,
        string diretorioArquivado,
        string caminhoEntradaCli) =>
        File.WriteAllTextAsync(
            Path.Combine(_diretorio, "resultado.md"),
            $$"""
            # Resultado do Worker black box

            - Processo: `Anamnesis.Worker.dll` em processo separado
            - Código de saída: `{{codigoSaida}}`
            - Reunião: `{{reuniaoId}}`
            - Estado final no SQLite: `{{statusFinal}}`

            ## Evidências

            - Configuração isolada: `{{caminhoConfiguracao}}`
            - Banco SQLite: `{{caminhoBanco}}`
            - stdout do Worker: `{{Path.Combine(_diretorio, "worker.stdout.log")}}`
            - stderr do Worker: `{{Path.Combine(_diretorio, "worker.stderr.log")}}`
            - JSON enviado à CLI: `{{caminhoEntradaCli}}`
            - Ata: `{{Path.Combine(diretorioArquivado, "ata.md")}}`
            - Transcrição: `{{Path.Combine(diretorioArquivado, "transcricao.md")}}`
            """);
}

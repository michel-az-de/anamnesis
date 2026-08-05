using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Arquivos;
using Anamnesis.Infrastructure.Cli;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Obs;
using Anamnesis.Infrastructure.Persistencia;
using Anamnesis.Infrastructure.Whisper;
using Anamnesis.Worker;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class FluxoAlphaE2ETests : IAsyncLifetime
{
    private string _diretorio = null!;
    private bool _preservarEvidencias;

    public Task InitializeAsync()
    {
        var diretorioEvidencias = Environment.GetEnvironmentVariable("ANAMNESIS_E2E_EVIDENCIAS");
        _preservarEvidencias = !string.IsNullOrWhiteSpace(diretorioEvidencias);
        _diretorio = _preservarEvidencias
            ? Path.GetFullPath(diretorioEvidencias!)
            : Path.Combine(Path.GetTempPath(), $"anamnesis-e2e-{Guid.NewGuid():N}");

        if (_preservarEvidencias && Directory.Exists(_diretorio) && Directory.EnumerateFileSystemEntries(_diretorio).Any())
        {
            throw new InvalidOperationException("O diretório de evidências deve estar vazio.");
        }

        Directory.CreateDirectory(_diretorio);
        return RegistrarAsync($"Ensaio iniciado. Diretório: {_diretorio}");
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
    public async Task DeveProcessarFluxoCompletoComInfraestruturaLocal()
    {
        var relogio = new RelogioManipulavel(new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero));
        var caminhoGravacao = Path.Combine(_diretorio, "gravacao.mkv");
        await File.WriteAllTextAsync(caminhoGravacao, "gravação de teste");
        await RegistrarAsync("Gravação de entrada criada.");
        await using var obs = new ServidorObsFake(caminhoGravacao);
        var caminhoBanco = Path.Combine(_diretorio, "anamnesis.db");
        var repository = new SqliteReuniaoRepository(caminhoBanco);
        var fila = new SqliteJobQueue(caminhoBanco);
        var controlar = new ControlarGravacaoHandler(
            repository,
            fila,
            new ObsGravador(new ObsWebSocketOptions(obs.Endereco, null)),
            new WorkerLauncherNulo(),
            relogio);

        var reuniaoId = await controlar.IniciarAsync("E2E da alpha", CancellationToken.None);
        await RegistrarAsync($"OBS recebeu StartRecord; reunião criada: {reuniaoId}.");
        await controlar.FinalizarAsync(reuniaoId, CancellationToken.None);
        await RegistrarAsync("OBS recebeu StopRecord; reunião e job persistidos no SQLite.");
        var reservadoAntesDoReinicio = await fila.ReservarProximoAsync(relogio.GetUtcNow(), CancellationToken.None);
        Assert.NotNull(reservadoAntesDoReinicio);
        await RegistrarAsync("Job reservado antes do reinício simulado.");

        var caminhoModelo = Path.Combine(_diretorio, "modelo.bin");
        await File.WriteAllTextAsync(caminhoModelo, "modelo de teste");
        var caminhoFfmpeg = await CriarFfmpegFakeAsync();
        var caminhoWhisper = await CriarWhisperFakeAsync();
        var (caminhoCli, caminhoEntradaCli) = await CriarCliFakeAsync();
        var processar = new ProcessarReuniaoHandler(
            repository,
            new WhisperTranscritor(new WhisperOptions(caminhoWhisper, caminhoModelo, "pt", caminhoFfmpeg)),
            new CliAtaRunner(new CliAtaRunnerOptions("CLI E2E", caminhoCli, [])),
            new DiscoArquivador(Path.Combine(_diretorio, "arquivo")),
            relogio);
        var consumer = new ReuniaoConsumer(fila, processar, relogio);

        await consumer.RetomarAsync(CancellationToken.None);
        await RegistrarAsync("Worker retomou a reserva pendente.");
        Assert.True(await consumer.ProcessarProximoAsync(CancellationToken.None));
        Assert.False(await consumer.ProcessarProximoAsync(CancellationToken.None));
        await RegistrarAsync("Worker executou transcrição local e geração de ata pela CLI local.");

        var arquivada = await repository.ObterAsync(reuniaoId, CancellationToken.None);
        Assert.NotNull(arquivada);
        Assert.Equal(StatusReuniao.Arquivada, arquivada!.Status);
        var diretorioReuniao = Path.Combine(_diretorio, "arquivo", "2026", "08", reuniaoId.ToString("N"));
        var caminhoAta = Path.Combine(diretorioReuniao, "ata.md");
        Assert.True(File.Exists(caminhoAta));
        Assert.True(File.Exists(Path.Combine(diretorioReuniao, "transcricao.md")));
        var ataMarkdown = await File.ReadAllTextAsync(caminhoAta);
        Assert.Contains("## Decisões", ataMarkdown);
        Assert.Contains("Decisao E2E.", ataMarkdown);
        Assert.Contains("## Tarefas", ataMarkdown);
        Assert.Contains("Tarefa E2E.", ataMarkdown);
        Assert.Equal(1, obs.Inicios);
        Assert.Equal(1, obs.Encerramentos);
        Assert.Contains("transcricao", await File.ReadAllTextAsync(caminhoEntradaCli), StringComparison.OrdinalIgnoreCase);
        await RegistrarAsync("Ata e transcrição arquivadas; protocolo OBS e entrada JSON da CLI confirmados.");

        relogio.Avancar(TimeSpan.FromDays(7));
        var lixeira = new LixeiraFake();
        var retencao = new RetencaoGravacaoHandler(repository, lixeira, relogio);
        var simulacao = await retencao.SimularAsync(reuniaoId, CancellationToken.None);
        Assert.True(simulacao.PodeMover);
        await retencao.AplicarAsync(reuniaoId, CancellationToken.None);

        var excluida = await repository.ObterAsync(reuniaoId, CancellationToken.None);
        Assert.Equal(StatusReuniao.Excluida, excluida!.Status);
        Assert.Equal(caminhoGravacao, lixeira.CaminhoMovido);
        await RegistrarAsync("Retenção aplicada pela lixeira fake; estado final Excluída confirmado.");
        await CriarResultadoAsync(reuniaoId, excluida.Status, caminhoBanco, caminhoGravacao, diretorioReuniao, caminhoEntradaCli);
        Assert.True(File.Exists(Path.Combine(_diretorio, "e2e.log")));
        Assert.True(File.Exists(Path.Combine(_diretorio, "resultado.md")));
    }

    private Task RegistrarAsync(string mensagem) =>
        File.AppendAllTextAsync(
            Path.Combine(_diretorio, "e2e.log"),
            $"{DateTimeOffset.UtcNow:O} | {mensagem}{Environment.NewLine}");

    private Task CriarResultadoAsync(
        Guid reuniaoId,
        StatusReuniao statusFinal,
        string caminhoBanco,
        string caminhoGravacao,
        string diretorioArquivado,
        string caminhoEntradaCli) =>
        File.WriteAllTextAsync(
            Path.Combine(_diretorio, "resultado.md"),
            $$"""
            # Resultado do ensaio E2E da alpha

            - Reunião: `{{reuniaoId}}`
            - Estado final: `{{statusFinal}}`
            - OBS: `StartRecord` e `StopRecord` confirmados
            - Worker: reserva retomada e job concluído
            - Retenção: lixeira fake acionada após arquivamento

            ## Evidências

            - Banco SQLite: `{{caminhoBanco}}`
            - Gravação de entrada: `{{caminhoGravacao}}`
            - Ata: `{{Path.Combine(diretorioArquivado, "ata.md")}}`
            - Transcrição: `{{Path.Combine(diretorioArquivado, "transcricao.md")}}`
            - JSON recebido pela CLI: `{{caminhoEntradaCli}}`
            - Log cronológico: `{{Path.Combine(_diretorio, "e2e.log")}}`
            """);

    private async Task<string> CriarWhisperFakeAsync()
    {
        var caminho = Path.Combine(_diretorio, "whisper-fake.cmd");
        await File.WriteAllTextAsync(caminho, """
            @echo off
            set "saida="
            :argumento
            if "%~1"=="" goto fim
            if "%~1"=="-of" (
                set "saida=%~2"
                shift
            )
            shift
            goto argumento
            :fim
            echo Transcrição E2E local> "%saida%.txt"
            """);
        return caminho;
    }

    private async Task<string> CriarFfmpegFakeAsync()
    {
        var caminho = Path.Combine(_diretorio, "ffmpeg-fake.cmd");
        await File.WriteAllTextAsync(caminho, "@echo off\r\nset \"saida=\"\r\n:argumento\r\nif \"%~1\"==\"\" goto fim\r\nset \"saida=%~1\"\r\nshift\r\ngoto argumento\r\n:fim\r\necho WAV E2E> \"%saida%\"\r\n");
        return caminho;
    }

    private async Task<(string CaminhoCli, string CaminhoEntrada)> CriarCliFakeAsync()
    {
        var caminhoEntrada = Path.Combine(_diretorio, "entrada-cli.json");
        var caminho = Path.Combine(_diretorio, "cli-fake.cmd");
        await File.WriteAllTextAsync(
            caminho,
            $"@echo off{Environment.NewLine}" +
            $"more > \"{caminhoEntrada}\"{Environment.NewLine}" +
            "echo {\"resumoExecutivo\":\"Resumo E2E.\",\"decisoes\":[\"Decisao E2E.\"],\"tarefas\":[{\"descricao\":\"Tarefa E2E.\",\"responsavel\":\"Ana\",\"prazo\":null}]}" +
            Environment.NewLine);
        return (caminho, caminhoEntrada);
    }

    private sealed class WorkerLauncherNulo : IWorkerLauncher
    {
        public Task IniciarAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class LixeiraFake : IGravacaoLixeira
    {
        public string? CaminhoMovido { get; private set; }

        public Task<bool> ExisteAsync(string caminhoArquivo, CancellationToken cancellationToken) => Task.FromResult(File.Exists(caminhoArquivo));

        public Task MoverAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            CaminhoMovido = caminhoArquivo;
            return Task.CompletedTask;
        }
    }

    private sealed class RelogioManipulavel(DateTimeOffset agora) : TimeProvider
    {
        private DateTimeOffset _agora = agora;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan intervalo) => _agora = _agora.Add(intervalo);
    }

    private sealed class ServidorObsFake : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Task _atendimento;
        private readonly string _caminhoGravacao;
        private int _inicios;
        private int _encerramentos;

        public ServidorObsFake(string caminhoGravacao)
        {
            _caminhoGravacao = caminhoGravacao;
            var porta = new TcpListener(IPAddress.Loopback, 0);
            porta.Start();
            var numeroPorta = ((IPEndPoint)porta.LocalEndpoint).Port;
            porta.Stop();
            Endereco = new Uri($"ws://127.0.0.1:{numeroPorta}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{numeroPorta}/");
            _listener.Start();
            _atendimento = AtenderAsync();
        }

        public Uri Endereco { get; }
        public int Inicios => Volatile.Read(ref _inicios);
        public int Encerramentos => Volatile.Read(ref _encerramentos);

        public async ValueTask DisposeAsync()
        {
            _listener.Close();
            try
            {
                await _atendimento;
            }
            catch (HttpListenerException)
            {
            }
        }

        private async Task AtenderAsync()
        {
            for (var conexao = 0; conexao < 2; conexao++)
            {
                var contexto = await _listener.GetContextAsync();
                using var socket = (await contexto.AcceptWebSocketAsync("obswebsocket.json")).WebSocket;
                await EnviarAsync(socket, new { op = 0, d = new { rpcVersion = 1 } });
                await ReceberAsync(socket);
                await EnviarAsync(socket, new { op = 2, d = new { negotiatedRpcVersion = 1 } });
                var solicitacao = await ReceberAsync(socket);
                var dados = solicitacao.GetProperty("d");
                var tipo = dados.GetProperty("requestType").GetString();
                var id = dados.GetProperty("requestId").GetString();
                object respostaDados = tipo == "StopRecord"
                    ? new { outputPath = _caminhoGravacao }
                    : new { };
                if (tipo == "StartRecord")
                {
                    Interlocked.Increment(ref _inicios);
                }
                else if (tipo == "StopRecord")
                {
                    Interlocked.Increment(ref _encerramentos);
                }

                await EnviarAsync(socket, new
                {
                    op = 7,
                    d = new
                    {
                        requestType = tipo,
                        requestId = id,
                        requestStatus = new { result = true, code = 100 },
                        responseData = respostaDados
                    }
                });
            }
        }

        private static async Task EnviarAsync(WebSocket socket, object mensagem)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(mensagem);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task<JsonElement> ReceberAsync(WebSocket socket)
        {
            var buffer = new byte[4096];
            using var mensagem = new MemoryStream();
            WebSocketReceiveResult resultado;
            do
            {
                resultado = await socket.ReceiveAsync(buffer, CancellationToken.None);
                mensagem.Write(buffer, 0, resultado.Count);
            }
            while (!resultado.EndOfMessage);

            using var documento = JsonDocument.Parse(mensagem.ToArray());
            return documento.RootElement.Clone();
        }
    }
}

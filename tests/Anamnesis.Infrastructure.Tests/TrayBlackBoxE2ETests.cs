using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Persistencia;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class TrayBlackBoxE2ETests
{
    [Fact]
    public async Task DeveGravarEAcionarWorkerEmProcessosSeparados()
    {
        var diretorioConfigurado = Environment.GetEnvironmentVariable("ANAMNESIS_TRAY_WORKER_E2E_EVIDENCIAS");
        var preservarEvidencias = !string.IsNullOrWhiteSpace(diretorioConfigurado);
        var diretorio = preservarEvidencias
            ? Path.GetFullPath(diretorioConfigurado!)
            : Path.Combine(Path.GetTempPath(), $"anamnesis-tray-e2e-{Guid.NewGuid():N}");
        if (preservarEvidencias && Directory.Exists(diretorio) && Directory.EnumerateFileSystemEntries(diretorio).Any())
        {
            throw new InvalidOperationException("O diretório de evidências deve estar vazio.");
        }

        Directory.CreateDirectory(diretorio);
        try
        {
            var caminhoGravacao = Path.Combine(diretorio, "gravacao.mkv");
            await File.WriteAllTextAsync(caminhoGravacao, "gravação do OBS fake");
            await using var obs = new ServidorObsFake(caminhoGravacao);
            var caminhoBanco = Path.Combine(diretorio, "anamnesis.db");
            var caminhoConfiguracao = Path.Combine(diretorio, "config.json");
            var caminhoModelo = Path.Combine(diretorio, "modelo.bin");
            await File.WriteAllTextAsync(caminhoModelo, "modelo fake");
            var caminhoFfmpeg = await CriarFfmpegFakeAsync(diretorio);
            var caminhoWhisper = await CriarWhisperFakeAsync(diretorio);
            var caminhoCli = await CriarCliFakeAsync(diretorio);
            var diretorioArquivo = Path.Combine(diretorio, "arquivo");
            await new ArquivoConfiguracao(caminhoConfiguracao).SalvarAsync(
                new ConfiguracaoAnamnesis
                {
                    CaminhoBanco = caminhoBanco,
                    DiretorioArquivo = diretorioArquivo,
                    EnderecoObs = obs.Endereco.ToString(),
                    CaminhoExecutavelFfmpeg = caminhoFfmpeg,
                    CaminhoExecutavelWhisper = caminhoWhisper,
                    CaminhoModeloWhisper = caminhoModelo,
                    CaminhoExecutavelCli = caminhoCli,
                    NomeCli = "CLI black box do Tray"
                },
                CancellationToken.None);

            var resultado = await ExecutarTrayAsync(caminhoConfiguracao, CaminhoWorker());

            Assert.Equal(0, resultado.CodigoSaida);
            Assert.Contains("Gravação de teste iniciada", resultado.Stdout, StringComparison.Ordinal);
            Assert.Contains("Gravação de teste concluída", resultado.Stdout, StringComparison.Ordinal);
            var linhaId = resultado.Stdout.Split(Environment.NewLine)
                .Single(linha => linha.StartsWith("ReuniaoId=", StringComparison.Ordinal));
            var reuniaoId = Guid.Parse(linhaId["ReuniaoId=".Length..]);
            var reuniao = await new SqliteReuniaoRepository(caminhoBanco).ObterAsync(reuniaoId, CancellationToken.None);
            Assert.NotNull(reuniao);
            reuniao = await AguardarArquivamentoAsync(caminhoBanco, reuniaoId);
            Assert.Equal(StatusReuniao.Arquivada, reuniao.Status);
            Assert.Equal(caminhoGravacao, reuniao.Gravacao?.CaminhoArquivo);
            var diretorioReuniao = Path.Combine(
                diretorioArquivo,
                reuniao.CriadaEm.ToString("yyyy", CultureInfo.InvariantCulture),
                reuniao.CriadaEm.ToString("MM", CultureInfo.InvariantCulture),
                reuniao.Id.ToString("N"));
            var caminhoAta = Path.Combine(diretorioReuniao, "ata.md");
            var caminhoTranscricao = Path.Combine(diretorioReuniao, "transcricao.md");
            Assert.True(File.Exists(caminhoAta));
            Assert.True(File.Exists(caminhoTranscricao));
            await File.WriteAllTextAsync(Path.Combine(diretorio, "tray-worker.stdout.log"), resultado.Stdout);
            await File.WriteAllTextAsync(Path.Combine(diretorio, "tray-worker.stderr.log"), resultado.Stderr);
            await File.WriteAllTextAsync(
                Path.Combine(diretorio, "resultado.md"),
                $$"""
                # Resultado Tray para Worker

                - Reunião: `{{reuniao.Id}}`
                - Estado final: `{{reuniao.Status}}`
                - Gravação: `{{caminhoGravacao}}`
                - Processo Tray: `Anamnesis.Tray.dll`
                - Processo Worker: `Anamnesis.Worker.exe`
                - OBS: fake local
                - Transcritor: fake local
                - CLI: fake local
                - Ata: `{{caminhoAta}}`
                - Transcrição: `{{caminhoTranscricao}}`
                """);
        }
        finally
        {
            if (!preservarEvidencias)
            {
                await ExcluirDiretorioQuandoLiberadoAsync(diretorio);
            }
        }
    }

    private static async Task ExcluirDiretorioQuandoLiberadoAsync(string diretorio)
    {
        for (var tentativa = 0; ; tentativa++)
        {
            try
            {
                Directory.Delete(diretorio, recursive: true);
                return;
            }
            catch (IOException) when (tentativa < 20)
            {
                await Task.Delay(250);
            }
        }
    }

    private static async Task<(int CodigoSaida, string Stdout, string Stderr)> ExecutarTrayAsync(
        string caminhoConfiguracao,
        string caminhoWorker)
    {
        using var processo = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
                Arguments = $"\"{Path.Combine(EncontrarRaizRepositorio(), "src", "Anamnesis.Tray", "bin", "Release", "net10.0-windows", "Anamnesis.Tray.dll")}\" --gravar-teste-segundos 1 --iniciar-worker",
                WorkingDirectory = EncontrarRaizRepositorio(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            }
        };
        processo.StartInfo.Environment["ANAMNESIS_CONFIGURACAO"] = caminhoConfiguracao;
        processo.StartInfo.Environment["ANAMNESIS_WORKER_EXECUTAVEL"] = caminhoWorker;
        processo.Start();
        var stdout = processo.StandardOutput.ReadToEndAsync();
        var stderr = processo.StandardError.ReadToEndAsync();
        var encerramento = processo.WaitForExitAsync();
        if (await Task.WhenAny(encerramento, Task.Delay(TimeSpan.FromSeconds(8))) != encerramento)
        {
            processo.Kill(entireProcessTree: true);
            await processo.WaitForExitAsync();
            throw new TimeoutException("O Tray não encerrou após o modo de validação.");
        }

        return (processo.ExitCode, await stdout, await stderr);
    }

    private static async Task<Anamnesis.Domain.Entidades.Reuniao> AguardarArquivamentoAsync(
        string caminhoBanco,
        Guid reuniaoId)
    {
        var repository = new SqliteReuniaoRepository(caminhoBanco);
        var limite = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < limite)
        {
            var reuniao = await repository.ObterAsync(reuniaoId, CancellationToken.None);
            if (reuniao?.Status == StatusReuniao.Arquivada)
            {
                return reuniao;
            }

            if (reuniao?.Status == StatusReuniao.Falha)
            {
                throw new InvalidOperationException($"O Worker registrou falha: {reuniao.MotivoFalha}");
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("O Worker não arquivou a reunião em dez segundos.");
    }

    private static string CaminhoWorker() => Path.Combine(
        EncontrarRaizRepositorio(),
        "src",
        "Anamnesis.Worker",
        "bin",
        "Release",
        "net10.0-windows",
        "Anamnesis.Worker.exe");

    private static Task<string> CriarFfmpegFakeAsync(string diretorio)
    {
        var caminho = Path.Combine(diretorio, "ffmpeg-fake.cmd");
        return CriarArquivoAsync(
            caminho,
            "@echo off\r\nset \"saida=\"\r\n:argumento\r\nif \"%~1\"==\"\" goto fim\r\nset \"saida=%~1\"\r\nshift\r\ngoto argumento\r\n:fim\r\necho WAV fake> \"%saida%\"\r\n");
    }

    private static Task<string> CriarWhisperFakeAsync(string diretorio)
    {
        var caminho = Path.Combine(diretorio, "whisper-fake.cmd");
        return CriarArquivoAsync(
            caminho,
            "@echo off\r\nset \"saida=\"\r\n:argumento\r\nif \"%~1\"==\"\" goto fim\r\nif \"%~1\"==\"-of\" (set \"saida=%~2\" & shift)\r\nshift\r\ngoto argumento\r\n:fim\r\necho Transcricao black box> \"%saida%.txt\"\r\n");
    }

    private static Task<string> CriarCliFakeAsync(string diretorio)
    {
        var caminho = Path.Combine(diretorio, "cli-fake.cmd");
        return CriarArquivoAsync(
            caminho,
            "@echo off\r\nmore > nul\r\necho {\"resumoExecutivo\":\"Resumo do Tray.\",\"decisoes\":[],\"tarefas\":[]}\r\n");
    }

    private static async Task<string> CriarArquivoAsync(string caminho, string conteudo)
    {
        await File.WriteAllTextAsync(caminho, conteudo);
        return caminho;
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

    private sealed class ServidorObsFake : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Task _atendimento;
        private readonly string _caminhoGravacao;

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
            catch (ObjectDisposedException)
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
                var quantidadeSolicitacoes = conexao == 0 ? 4 : 2;
                for (var indice = 0; indice < quantidadeSolicitacoes; indice++)
                {
                    var solicitacao = await ReceberAsync(socket);
                    var dados = solicitacao.GetProperty("d");
                    var tipo = dados.GetProperty("requestType").GetString();
                    var id = dados.GetProperty("requestId").GetString();
                    object respostaDados = tipo switch
                    {
                        "GetSceneList" => new
                        {
                            currentProgramSceneName = "Cena",
                            scenes = new[] { new { sceneName = "Cena" }, new { sceneName = "Anamnesis" } }
                        },
                        "GetInputList" => new
                        {
                            inputs = new[]
                            {
                                new { inputName = "Anamnesis | Audio do sistema" },
                                new { inputName = "Anamnesis | Microfone" }
                            }
                        },
                        "StopRecord" => new { outputPath = _caminhoGravacao },
                        _ => new { }
                    };
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
        }

        private static Task EnviarAsync(WebSocket socket, object mensagem)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(mensagem);
            return socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
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

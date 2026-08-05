using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Persistencia;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class TrayBlackBoxE2ETests
{
    [Fact]
    public async Task DeveGravarTemporizadoEmProcessoSeparadoEPersistirJob()
    {
        var diretorio = Path.Combine(Path.GetTempPath(), $"anamnesis-tray-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(diretorio);
        try
        {
            var caminhoGravacao = Path.Combine(diretorio, "gravacao.mkv");
            await File.WriteAllTextAsync(caminhoGravacao, "gravação do OBS fake");
            await using var obs = new ServidorObsFake(caminhoGravacao);
            var caminhoBanco = Path.Combine(diretorio, "anamnesis.db");
            var caminhoConfiguracao = Path.Combine(diretorio, "config.json");
            await new ArquivoConfiguracao(caminhoConfiguracao).SalvarAsync(
                new ConfiguracaoAnamnesis
                {
                    CaminhoBanco = caminhoBanco,
                    DiretorioArquivo = Path.Combine(diretorio, "arquivo"),
                    EnderecoObs = obs.Endereco.ToString()
                },
                CancellationToken.None);

            var resultado = await ExecutarTrayAsync(caminhoConfiguracao);

            Assert.Equal(0, resultado.CodigoSaida);
            Assert.Contains("Gravação de teste iniciada", resultado.Stdout, StringComparison.Ordinal);
            Assert.Contains("Gravação de teste concluída", resultado.Stdout, StringComparison.Ordinal);
            var linhaId = resultado.Stdout.Split(Environment.NewLine)
                .Single(linha => linha.StartsWith("ReuniaoId=", StringComparison.Ordinal));
            var reuniaoId = Guid.Parse(linhaId["ReuniaoId=".Length..]);
            var reuniao = await new SqliteReuniaoRepository(caminhoBanco).ObterAsync(reuniaoId, CancellationToken.None);
            Assert.NotNull(reuniao);
            Assert.Equal(caminhoGravacao, reuniao!.Gravacao?.CaminhoArquivo);
            var job = await new SqliteJobQueue(caminhoBanco).ReservarProximoAsync(DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.Equal(reuniaoId, job?.ReuniaoId);
        }
        finally
        {
            Directory.Delete(diretorio, recursive: true);
        }
    }

    private static async Task<(int CodigoSaida, string Stdout, string Stderr)> ExecutarTrayAsync(string caminhoConfiguracao)
    {
        using var processo = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
                Arguments = $"\"{Path.Combine(EncontrarRaizRepositorio(), "src", "Anamnesis.Tray", "bin", "Release", "net10.0-windows", "Anamnesis.Tray.dll")}\" --gravar-teste-segundos 1",
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
                var solicitacao = await ReceberAsync(socket);
                var dados = solicitacao.GetProperty("d");
                var tipo = dados.GetProperty("requestType").GetString();
                var id = dados.GetProperty("requestId").GetString();
                object respostaDados = tipo == "StopRecord" ? new { outputPath = _caminhoGravacao } : new { };
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

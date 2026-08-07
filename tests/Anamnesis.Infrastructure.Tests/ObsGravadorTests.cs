using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using Anamnesis.Infrastructure.Obs;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class ObsGravadorTests
{
    [Fact]
    public async Task DeveConsultarSeObsPossuiGravacaoAtiva()
    {
        await using var servidor = new ServidorObsFake([
            [new(new { outputActive = true })]
        ]);
        var gravador = new ObsGravador(new ObsWebSocketOptions(servidor.Endereco, null));

        var estaGravando = await gravador.EstaGravandoAsync(CancellationToken.None);

        Assert.True(estaGravando);
        Assert.Equal(["GetRecordStatus"], servidor.TiposSolicitados);
    }

    [Fact]
    public async Task DeveEncerrarMesmoQuandoRestauracaoDeCenaNaoResponde()
    {
        var caminhoGravacao = Path.Combine(Path.GetTempPath(), $"anamnesis-{Guid.NewGuid():N}.mkv");
        await using var servidor = new ServidorObsFake([
            [
                new(new { currentProgramSceneName = "Cena pessoal", scenes = new[] { new { sceneName = "Cena pessoal" } } }),
                new(new { inputs = Array.Empty<object>() }),
                new(new { }),
                new(new { }),
                new(new { inputUuid = Guid.NewGuid(), sceneItemId = 1 }),
                new(new { inputUuid = Guid.NewGuid(), sceneItemId = 2 }),
                new(new { }),
                new(new { })
            ],
            [
                new(new { outputPath = caminhoGravacao }),
                new(new { }, Silenciosa: true)
            ]
        ]);
        var gravador = new ObsGravador(new ObsWebSocketOptions(servidor.Endereco, null));
        await gravador.IniciarAsync(CancellationToken.None);

        var finalizacao = gravador.FinalizarAsync(CancellationToken.None);
        var concluiu = await Task.WhenAny(finalizacao, Task.Delay(TimeSpan.FromSeconds(30))) == finalizacao;

        Assert.True(concluiu, "FinalizarAsync ficou preso restaurando a cena de um OBS que parou de responder.");
        Assert.Equal(caminhoGravacao, await finalizacao);
    }

    [Fact]
    public async Task DevePrepararCenaEFontesAntesDeGravarERestaurarDepois()
    {
        var caminhoGravacao = Path.Combine(Path.GetTempPath(), $"anamnesis-{Guid.NewGuid():N}.mkv");
        await using var servidor = new ServidorObsFake([
            [
                new(new { currentProgramSceneName = "Cena pessoal", scenes = new[] { new { sceneName = "Cena pessoal" } } }),
                new(new { inputs = Array.Empty<object>() }),
                new(new { }),
                new(new { }),
                new(new { inputUuid = Guid.NewGuid(), sceneItemId = 1 }),
                new(new { inputUuid = Guid.NewGuid(), sceneItemId = 2 }),
                new(new { }),
                new(new { })
            ],
            [
                new(new { outputPath = caminhoGravacao }),
                new(new { })
            ]
        ]);
        var gravador = new ObsGravador(new ObsWebSocketOptions(servidor.Endereco, null));

        await gravador.IniciarAsync(CancellationToken.None);
        var caminho = await gravador.FinalizarAsync(CancellationToken.None);

        Assert.Equal(caminhoGravacao, caminho);
        Assert.Equal([
            "GetSceneList",
            "GetInputList",
            "GetSpecialInputs",
            "CreateScene",
            "CreateInput",
            "CreateInput",
            "SetCurrentProgramScene",
            "StartRecord",
            "StopRecord",
            "SetCurrentProgramScene"
        ], servidor.TiposSolicitados);
        var criacaoSistema = servidor.Solicitacoes[4].GetProperty("d").GetProperty("requestData");
        Assert.Equal("Anamnesis", criacaoSistema.GetProperty("sceneName").GetString());
        Assert.Equal("Anamnesis | Audio do sistema", criacaoSistema.GetProperty("inputName").GetString());
        Assert.Equal("wasapi_output_capture", criacaoSistema.GetProperty("inputKind").GetString());
        Assert.Equal("default", criacaoSistema.GetProperty("inputSettings").GetProperty("device_id").GetString());
        var criacaoMicrofone = servidor.Solicitacoes[5].GetProperty("d").GetProperty("requestData");
        Assert.Equal("Anamnesis | Microfone", criacaoMicrofone.GetProperty("inputName").GetString());
        Assert.Equal("wasapi_input_capture", criacaoMicrofone.GetProperty("inputKind").GetString());
        var restauracao = servidor.Solicitacoes[9].GetProperty("d").GetProperty("requestData");
        Assert.Equal("Cena pessoal", restauracao.GetProperty("sceneName").GetString());
    }

    [Fact]
    public async Task DeveReutilizarCenaEFontesExistentes()
    {
        await using var servidor = new ServidorObsFake([
            [
                new(new
                {
                    currentProgramSceneName = "Cena pessoal",
                    scenes = new[] { new { sceneName = "Cena pessoal" }, new { sceneName = "Anamnesis" } }
                }),
                new(new
                {
                    inputs = new[]
                    {
                        new { inputName = "Anamnesis | Audio do sistema", inputKind = "wasapi_output_capture" },
                        new { inputName = "Anamnesis | Microfone", inputKind = "wasapi_input_capture" }
                    }
                }),
                new(new { }),
                new(new { }),
                new(new { })
            ],
            [
                new(new { outputPath = "C:\\gravacoes\\reuniao.mkv" }),
                new(new { })
            ]
        ]);
        var gravador = new ObsGravador(new ObsWebSocketOptions(servidor.Endereco, null));

        await gravador.IniciarAsync(CancellationToken.None);
        await gravador.FinalizarAsync(CancellationToken.None);

        Assert.Equal([
            "GetSceneList",
            "GetInputList",
            "GetSpecialInputs",
            "SetCurrentProgramScene",
            "StartRecord",
            "StopRecord",
            "SetCurrentProgramScene"
        ], servidor.TiposSolicitados);
    }

    [Fact]
    public async Task DeveRestaurarCenaMesmoQuandoObsRecusaEncerramento()
    {
        await using var servidor = new ServidorObsFake([
            [
                new(new
                {
                    currentProgramSceneName = "Cena pessoal",
                    scenes = new[] { new { sceneName = "Cena pessoal" }, new { sceneName = "Anamnesis" } }
                }),
                new(new
                {
                    inputs = new[]
                    {
                        new { inputName = "Anamnesis | Audio do sistema" },
                        new { inputName = "Anamnesis | Microfone" }
                    }
                }),
                new(new { }),
                new(new { }),
                new(new { })
            ],
            [
                new(new { }, Sucesso: false),
                new(new { })
            ]
        ]);
        var gravador = new ObsGravador(new ObsWebSocketOptions(servidor.Endereco, null));
        await gravador.IniciarAsync(CancellationToken.None);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gravador.FinalizarAsync(CancellationToken.None));

        Assert.Contains("StopRecord", excecao.Message, StringComparison.Ordinal);
        Assert.Equal("SetCurrentProgramScene", servidor.TiposSolicitados[^1]);
    }

    [Fact]
    public async Task DeveIgnorarEventoAssincronoEnquantoAguardaRespostaDaSolicitacao()
    {
        await using var servidor = new ServidorObsFake([
            [
                new(new
                {
                    currentProgramSceneName = "Cena pessoal",
                    scenes = new[] { new { sceneName = "Cena pessoal" }, new { sceneName = "Anamnesis" } }
                }),
                new(new { inputs = Array.Empty<object>() }),
                new(new { }),
                new(new { inputUuid = Guid.NewGuid(), sceneItemId = 1 }, EmitirEventoAntes: true),
                new(new { inputUuid = Guid.NewGuid(), sceneItemId = 2 }, EmitirEventoAntes: true),
                new(new { }),
                new(new { })
            ],
            [
                new(new { outputPath = "C:\\gravacoes\\reuniao.mkv" }),
                new(new { })
            ]
        ]);
        var gravador = new ObsGravador(new ObsWebSocketOptions(servidor.Endereco, null));

        await gravador.IniciarAsync(CancellationToken.None);
        var caminho = await gravador.FinalizarAsync(CancellationToken.None);

        Assert.Equal("C:\\gravacoes\\reuniao.mkv", caminho);
        Assert.Equal(9, servidor.TiposSolicitados.Length);
    }

    [Fact]
    public async Task DeveRemoverFontesGerenciadasQuandoEntradasGlobaisJaCapturamAudio()
    {
        await using var servidor = new ServidorObsFake([
            [
                new(new
                {
                    currentProgramSceneName = "Cena pessoal",
                    scenes = new[] { new { sceneName = "Cena pessoal" }, new { sceneName = "Anamnesis" } }
                }),
                new(new
                {
                    inputs = new[]
                    {
                        new { inputName = "Áudio do desktop", inputKind = "wasapi_output_capture" },
                        new { inputName = "Mic/Aux", inputKind = "wasapi_input_capture" },
                        new { inputName = "Anamnesis | Audio do sistema", inputKind = "wasapi_output_capture" },
                        new { inputName = "Anamnesis | Microfone", inputKind = "wasapi_input_capture" }
                    }
                }),
                new(new { desktop1 = "Áudio do desktop", mic1 = "Mic/Aux" }),
                new(new { }),
                new(new { }),
                new(new { }),
                new(new { })
            ],
            [
                new(new { outputPath = "C:\\gravacoes\\sem-duplicacao.mkv" }),
                new(new { })
            ]
        ]);
        var gravador = new ObsGravador(new ObsWebSocketOptions(servidor.Endereco, null));

        await gravador.IniciarAsync(CancellationToken.None);
        await gravador.FinalizarAsync(CancellationToken.None);

        Assert.Equal([
            "GetSceneList",
            "GetInputList",
            "GetSpecialInputs",
            "RemoveInput",
            "RemoveInput",
            "SetCurrentProgramScene",
            "StartRecord",
            "StopRecord",
            "SetCurrentProgramScene"
        ], servidor.TiposSolicitados);
        Assert.Equal(
            "Anamnesis | Audio do sistema",
            servidor.Solicitacoes[3].GetProperty("d").GetProperty("requestData").GetProperty("inputName").GetString());
        Assert.Equal(
            "Anamnesis | Microfone",
            servidor.Solicitacoes[4].GetProperty("d").GetProperty("requestData").GetProperty("inputName").GetString());
    }

    private sealed record Resposta(
        object Dados,
        bool Sucesso = true,
        bool EmitirEventoAntes = false,
        bool Silenciosa = false);

    private sealed class ServidorObsFake : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly IReadOnlyList<IReadOnlyList<Resposta>> _conexoes;
        private readonly Task _atendimento;

        public ServidorObsFake(IReadOnlyList<IReadOnlyList<Resposta>> conexoes)
        {
            _conexoes = conexoes;
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
        public List<JsonElement> Solicitacoes { get; } = [];
        public string[] TiposSolicitados => Solicitacoes
            .Select(solicitacao => solicitacao.GetProperty("d").GetProperty("requestType").GetString()!)
            .ToArray();

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
            foreach (var respostas in _conexoes)
            {
                var contexto = await _listener.GetContextAsync();
                using var socket = (await contexto.AcceptWebSocketAsync("obswebsocket.json")).WebSocket;
                await EnviarAsync(socket, new { op = 0, d = new { rpcVersion = 1 } });
                await ReceberAsync(socket);
                await EnviarAsync(socket, new { op = 2, d = new { negotiatedRpcVersion = 1 } });
                var indiceResposta = 0;
                while (socket.State == WebSocketState.Open)
                {
                    JsonElement? recebida;
                    try
                    {
                        recebida = await TentarReceberAsync(socket);
                    }
                    catch (WebSocketException)
                    {
                        break;
                    }

                    if (recebida is null)
                    {
                        break;
                    }

                    var solicitacao = recebida.Value;
                    Solicitacoes.Add(solicitacao);
                    var dados = solicitacao.GetProperty("d");
                    var resposta = respostas[indiceResposta++];
                    if (resposta.Silenciosa)
                    {
                        // O OBS recebeu a solicitação e travou: nunca responde.
                        continue;
                    }

                    if (resposta.EmitirEventoAntes)
                    {
                        await EnviarAsync(socket, new
                        {
                            op = 5,
                            d = new { eventType = "InputCreated", eventIntent = 8, eventData = new { } }
                        });
                    }

                    await EnviarAsync(socket, new
                    {
                        op = 7,
                        d = new
                        {
                            requestType = dados.GetProperty("requestType").GetString(),
                            requestId = dados.GetProperty("requestId").GetString(),
                            requestStatus = new { result = resposta.Sucesso, code = resposta.Sucesso ? 100 : 700 },
                            responseData = resposta.Dados
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
            return await TentarReceberAsync(socket)
                ?? throw new InvalidOperationException("O cliente OBS encerrou a conexão antes da mensagem esperada.");
        }

        private static async Task<JsonElement?> TentarReceberAsync(WebSocket socket)
        {
            var buffer = new byte[8192];
            using var mensagem = new MemoryStream();
            WebSocketReceiveResult resultado;
            do
            {
                resultado = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (resultado.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                mensagem.Write(buffer, 0, resultado.Count);
            }
            while (!resultado.EndOfMessage);

            using var documento = JsonDocument.Parse(mensagem.ToArray());
            return documento.RootElement.Clone();
        }
    }
}

using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anamnesis.Application.Contracts;

namespace Anamnesis.Infrastructure.Obs;

public sealed class ObsGravador(ObsWebSocketOptions options) : IGravador
{
    private const string CenaAnamnesis = "Anamnesis";
    private const string AudioSistema = "Anamnesis | Audio do sistema";
    private const string Microfone = "Anamnesis | Microfone";
    private string? _cenaAnterior;

    public async Task IniciarAsync(CancellationToken cancellationToken)
    {
        using var cliente = await ConectarAsync(cancellationToken);
        var cenas = await EnviarSolicitacaoAsync(cliente, "GetSceneList", null, cancellationToken);
        var dadosCenas = cenas.GetProperty("responseData");
        _cenaAnterior = dadosCenas.GetProperty("currentProgramSceneName").GetString();
        var cenaExiste = dadosCenas.GetProperty("scenes").EnumerateArray().Any(cena =>
            string.Equals(cena.GetProperty("sceneName").GetString(), CenaAnamnesis, StringComparison.Ordinal));

        var entradas = await EnviarSolicitacaoAsync(cliente, "GetInputList", null, cancellationToken);
        var nomesEntradas = entradas.GetProperty("responseData").GetProperty("inputs")
            .EnumerateArray()
            .Select(entrada => entrada.GetProperty("inputName").GetString())
            .Where(nome => nome is not null)
            .ToHashSet(StringComparer.Ordinal);

        if (!cenaExiste)
        {
            await EnviarSolicitacaoAsync(
                cliente,
                "CreateScene",
                new { sceneName = CenaAnamnesis },
                cancellationToken);
        }

        if (!nomesEntradas.Contains(AudioSistema))
        {
            await CriarEntradaAsync(cliente, AudioSistema, "wasapi_output_capture", cancellationToken);
        }

        if (!nomesEntradas.Contains(Microfone))
        {
            await CriarEntradaAsync(cliente, Microfone, "wasapi_input_capture", cancellationToken);
        }

        var deveRestaurar = !string.Equals(_cenaAnterior, CenaAnamnesis, StringComparison.Ordinal);
        try
        {
            if (deveRestaurar)
            {
                await EnviarSolicitacaoAsync(
                    cliente,
                    "SetCurrentProgramScene",
                    new { sceneName = CenaAnamnesis },
                    cancellationToken);
            }

            await EnviarSolicitacaoAsync(cliente, "StartRecord", null, cancellationToken);
        }
        catch
        {
            if (deveRestaurar)
            {
                await RestaurarCenaAsync(cliente, CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<string> FinalizarAsync(CancellationToken cancellationToken)
    {
        using var cliente = await ConectarAsync(cancellationToken);
        try
        {
            var resposta = await EnviarSolicitacaoAsync(cliente, "StopRecord", null, cancellationToken);
            var caminhoArquivo = resposta.GetProperty("responseData").GetProperty("outputPath").GetString();
            return !string.IsNullOrWhiteSpace(caminhoArquivo)
                ? caminhoArquivo
                : throw new InvalidOperationException("OBS não retornou o caminho da gravação encerrada.");
        }
        finally
        {
            try
            {
                await RestaurarCenaAsync(cliente, CancellationToken.None);
            }
            catch
            {
                // A gravação encerrada e seu caminho têm prioridade sobre a restauração visual do OBS.
            }
        }
    }

    private static Task<JsonElement> CriarEntradaAsync(
        ClientWebSocket cliente,
        string nome,
        string tipo,
        CancellationToken cancellationToken) =>
        EnviarSolicitacaoAsync(
            cliente,
            "CreateInput",
            new
            {
                sceneName = CenaAnamnesis,
                inputName = nome,
                inputKind = tipo,
                inputSettings = new { device_id = "default" },
                sceneItemEnabled = true
            },
            cancellationToken);

    private async Task RestaurarCenaAsync(ClientWebSocket cliente, CancellationToken cancellationToken)
    {
        var cenaAnterior = _cenaAnterior;
        _cenaAnterior = null;
        if (string.IsNullOrWhiteSpace(cenaAnterior) ||
            string.Equals(cenaAnterior, CenaAnamnesis, StringComparison.Ordinal))
        {
            return;
        }

        await EnviarSolicitacaoAsync(
            cliente,
            "SetCurrentProgramScene",
            new { sceneName = cenaAnterior },
            cancellationToken);
    }

    private async Task<ClientWebSocket> ConectarAsync(CancellationToken cancellationToken)
    {
        var cliente = new ClientWebSocket();
        cliente.Options.AddSubProtocol("obswebsocket.json");

        try
        {
            await cliente.ConnectAsync(options.Endereco, cancellationToken);
            var hello = await ReceberAsync(cliente, cancellationToken);
            if (hello.GetProperty("op").GetInt32() != 0)
            {
                throw new InvalidOperationException("OBS não enviou a mensagem de início esperada.");
            }

            var dadosHello = hello.GetProperty("d");
            var identificacao = new Dictionary<string, object?>
            {
                ["rpcVersion"] = dadosHello.GetProperty("rpcVersion").GetInt32(),
                ["eventSubscriptions"] = 0
            };

            if (dadosHello.TryGetProperty("authentication", out var autenticacao))
            {
                identificacao["authentication"] = CriarAutenticacao(
                    options.Senha ?? throw new InvalidOperationException("OBS exige senha, mas ela não foi configurada."),
                    autenticacao.GetProperty("salt").GetString()!,
                    autenticacao.GetProperty("challenge").GetString()!);
            }

            await EnviarAsync(cliente, new { op = 1, d = identificacao }, cancellationToken);
            var identificado = await ReceberAsync(cliente, cancellationToken);
            if (identificado.GetProperty("op").GetInt32() != 2)
            {
                throw new InvalidOperationException("OBS recusou a identificação do Anamnesis.");
            }

            return cliente;
        }
        catch
        {
            cliente.Dispose();
            throw;
        }
    }

    private static async Task<JsonElement> EnviarSolicitacaoAsync(
        ClientWebSocket cliente,
        string tipo,
        object? dadosSolicitacao,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("N");
        var envelopeSolicitacao = new Dictionary<string, object?>
        {
            ["requestType"] = tipo,
            ["requestId"] = id
        };
        if (dadosSolicitacao is not null)
        {
            envelopeSolicitacao["requestData"] = dadosSolicitacao;
        }

        await EnviarAsync(cliente, new { op = 6, d = envelopeSolicitacao }, cancellationToken);
        JsonElement dados;
        while (true)
        {
            var resposta = await ReceberAsync(cliente, cancellationToken);
            if (!resposta.TryGetProperty("op", out var operacao) || operacao.GetInt32() != 7 ||
                !resposta.TryGetProperty("d", out dados) ||
                !dados.TryGetProperty("requestId", out var idResposta) ||
                !string.Equals(idResposta.GetString(), id, StringComparison.Ordinal))
            {
                continue;
            }

            break;
        }

        var status = dados.GetProperty("requestStatus");
        if (!status.GetProperty("result").GetBoolean())
        {
            var codigo = status.TryGetProperty("code", out var valorCodigo) ? valorCodigo.GetInt32() : 0;
            throw new InvalidOperationException($"OBS recusou a solicitação '{tipo}' (código {codigo}).");
        }

        return dados;
    }

    private static async Task EnviarAsync(ClientWebSocket cliente, object mensagem, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(mensagem);
        await cliente.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<JsonElement> ReceberAsync(ClientWebSocket cliente, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var mensagem = new MemoryStream();
        WebSocketReceiveResult resultado;

        do
        {
            resultado = await cliente.ReceiveAsync(buffer, cancellationToken);
            if (resultado.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("OBS encerrou a conexão WebSocket.");
            }

            mensagem.Write(buffer, 0, resultado.Count);
        }
        while (!resultado.EndOfMessage);

        using var documento = JsonDocument.Parse(mensagem.ToArray());
        return documento.RootElement.Clone();
    }

    private static string CriarAutenticacao(string senha, string salt, string challenge)
    {
        var segredo = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{senha}{salt}")));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{segredo}{challenge}")));
    }
}

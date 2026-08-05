using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Anamnesis.Application.Contracts;

namespace Anamnesis.Infrastructure.Obs;

public sealed class ObsGravador(ObsWebSocketOptions options) : IGravador
{
    public Task IniciarAsync(CancellationToken cancellationToken) =>
        ExecutarSolicitacaoAsync("StartRecord", cancellationToken);

    public async Task<string> FinalizarAsync(CancellationToken cancellationToken)
    {
        using var cliente = await ConectarAsync(cancellationToken);
        var resposta = await EnviarSolicitacaoAsync(cliente, "StopRecord", cancellationToken);
        var caminhoArquivo = resposta.GetProperty("responseData").GetProperty("outputPath").GetString();
        return !string.IsNullOrWhiteSpace(caminhoArquivo)
            ? caminhoArquivo
            : throw new InvalidOperationException("OBS não retornou o caminho da gravação encerrada.");
    }

    private async Task ExecutarSolicitacaoAsync(string tipo, CancellationToken cancellationToken)
    {
        using var cliente = await ConectarAsync(cancellationToken);
        await EnviarSolicitacaoAsync(cliente, tipo, cancellationToken);
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
                ["rpcVersion"] = dadosHello.GetProperty("rpcVersion").GetInt32()
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

    private static async Task<JsonElement> EnviarSolicitacaoAsync(ClientWebSocket cliente, string tipo, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("N");
        await EnviarAsync(cliente, new { op = 6, d = new { requestType = tipo, requestId = id } }, cancellationToken);
        var resposta = await ReceberAsync(cliente, cancellationToken);
        var dados = resposta.GetProperty("d");
        var status = dados.GetProperty("requestStatus");
        if (resposta.GetProperty("op").GetInt32() != 7 || !status.GetProperty("result").GetBoolean())
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

namespace Anamnesis.Infrastructure.Obs;

public sealed record ObsWebSocketOptions(Uri Endereco, string? Senha)
{
    public static ObsWebSocketOptions Padrao { get; } = new(new Uri("ws://127.0.0.1:4455"), null);
}

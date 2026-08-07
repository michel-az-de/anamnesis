namespace Anamnesis.Infrastructure.Cli;

public sealed class AtaJsonInvalidoException : InvalidOperationException
{
    public AtaJsonInvalidoException(string mensagem)
        : base(mensagem)
    {
    }

    public AtaJsonInvalidoException(string mensagem, Exception innerException)
        : base(mensagem, innerException)
    {
    }
}

namespace Anamnesis.Application.UseCases;

public sealed class WorkerNaoIniciadoException(string mensagem, Exception innerException)
    : Exception(mensagem, innerException);

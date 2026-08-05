namespace Anamnesis.Application.UseCases;

public sealed class GravacaoJaAtivaException(Exception? innerException = null)
    : InvalidOperationException("Já existe uma reunião sendo gravada.", innerException);


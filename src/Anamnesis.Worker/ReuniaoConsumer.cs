namespace Anamnesis.Worker;

public sealed class ReuniaoConsumer
{
    // O primeiro incremento buscará jobs pendentes no SQLite e chamará ProcessarReuniaoHandler.
    // A fila permanece local e durável; não há dependência de infraestrutura externa.
}

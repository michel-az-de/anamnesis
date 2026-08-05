namespace Anamnesis.Infrastructure.Whisper;

public interface IDockerPreflight
{
    Task PrepararAsync(CancellationToken cancellationToken);
}

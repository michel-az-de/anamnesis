namespace Anamnesis.Application.Modelos;

public sealed record NivelAudioLeitura(
    int? Sistema,
    int? Microfone,
    string? MotivoIndisponibilidade = null)
{
    public static NivelAudioLeitura SemLeitura(string? motivo = null) =>
        new(null, null, motivo ?? "Leitura de nível de áudio indisponível.");
}

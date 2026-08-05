namespace Anamnesis.Application.Modelos;

public sealed record ArtefatosReuniao(
    Guid ReuniaoId,
    string Diretorio,
    string CaminhoAta,
    string CaminhoTranscricao);

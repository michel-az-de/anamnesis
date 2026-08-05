using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;

namespace Anamnesis.Application.Modelos;

public sealed record ReuniaoDetalhe(
    Guid Id,
    string Titulo,
    DateTimeOffset CriadaEm,
    StatusReuniao Status,
    string? MotivoFalha,
    string? CaminhoGravacao,
    DateTimeOffset? GravacaoIniciadaEm,
    DateTimeOffset? GravacaoFinalizadaEm,
    string? TextoTranscricao,
    string? IdiomaTranscricao,
    DateTimeOffset? TranscricaoGeradaEm,
    string? ResumoExecutivo,
    IReadOnlyList<string> Decisoes,
    IReadOnlyList<Tarefa> Tarefas,
    DateTimeOffset? AtaGeradaEm,
    DateTimeOffset? ArquivadaEm,
    ArtefatosReuniao? Artefatos);


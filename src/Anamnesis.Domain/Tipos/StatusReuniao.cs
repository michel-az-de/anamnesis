namespace Anamnesis.Domain.Tipos;

public enum StatusReuniao
{
    Agendada,
    Gravando,
    AguardandoProcessamento,
    EmTranscricao,
    EmAnalise,
    AguardandoArquivamento,
    Arquivada,
    PendenteExclusao,
    Excluida,
    Falha
}

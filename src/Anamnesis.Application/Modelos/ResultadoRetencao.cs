namespace Anamnesis.Application.Modelos;

public enum MotivoRetencao
{
    Elegivel,
    ReuniaoNaoArquivada,
    PrazoNaoAtingido,
    GravacaoNaoEncontrada
}

public sealed record ResultadoRetencao(MotivoRetencao Motivo, string? CaminhoArquivo)
{
    public bool PodeMover => Motivo is MotivoRetencao.Elegivel;

    /// <summary>
    /// Texto de apresentação do motivo. A decisão de fluxo é do enum: comparar esta string
    /// faria uma correção de redação virar mudança de comportamento.
    /// </summary>
    public string? Descricao => Motivo switch
    {
        MotivoRetencao.ReuniaoNaoArquivada => "A reunião não está arquivada.",
        MotivoRetencao.PrazoNaoAtingido => "O prazo de retenção ainda não foi atingido.",
        MotivoRetencao.GravacaoNaoEncontrada => "A gravação não foi encontrada.",
        _ => null
    };
}

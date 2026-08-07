namespace Anamnesis.Tray;

internal enum EtapaFluxoOperacional
{
    Preparando,
    Gravando,
    ArquivoSalvo,
    NaFila,
    Transcrevendo,
    GerandoAta,
    Arquivando,
    Concluido,
    Falha
}

internal enum EstadoItemEtapa
{
    Pendente,
    Concluida,
    Atual
}

internal sealed record ItemEtapaOperacional(
    EtapaFluxoOperacional Etapa,
    string Nome,
    EstadoItemEtapa Estado);

internal sealed record EstadoFluxoOperacional(
    EtapaFluxoOperacional Atual,
    IReadOnlyList<ItemEtapaOperacional> Itens,
    bool Carregando);

internal static class FluxoOperacionalDesktop
{
    private static readonly (EtapaFluxoOperacional Etapa, string Nome)[] Sequencia =
    [
        (EtapaFluxoOperacional.Preparando, "Preparando"),
        (EtapaFluxoOperacional.Gravando, "Gravando"),
        (EtapaFluxoOperacional.ArquivoSalvo, "Arquivo salvo"),
        (EtapaFluxoOperacional.NaFila, "Na fila"),
        (EtapaFluxoOperacional.Transcrevendo, "Transcrevendo"),
        (EtapaFluxoOperacional.GerandoAta, "Gerando ata"),
        (EtapaFluxoOperacional.Arquivando, "Arquivando"),
        (EtapaFluxoOperacional.Concluido, "Concluído"),
        (EtapaFluxoOperacional.Falha, "Falha")
    ];

    public static EstadoFluxoOperacional Criar(string? status)
    {
        var atual = status switch
        {
            "Gravando" => EtapaFluxoOperacional.Gravando,
            "Arquivo salvo" => EtapaFluxoOperacional.ArquivoSalvo,
            "Aguardando processamento" => EtapaFluxoOperacional.NaFila,
            "Transcrevendo" => EtapaFluxoOperacional.Transcrevendo,
            "Gerando ata" => EtapaFluxoOperacional.GerandoAta,
            "Arquivando" => EtapaFluxoOperacional.Arquivando,
            "Ata pronta" => EtapaFluxoOperacional.Concluido,
            "Falha" => EtapaFluxoOperacional.Falha,
            _ => EtapaFluxoOperacional.Preparando
        };
        var indiceAtual = Array.FindIndex(Sequencia, item => item.Etapa == atual);
        var falhou = atual == EtapaFluxoOperacional.Falha;
        var itens = Sequencia.Select((item, indice) => new ItemEtapaOperacional(
            item.Etapa,
            item.Nome,
            item.Etapa == atual
                ? EstadoItemEtapa.Atual
                : !falhou && indice < indiceAtual
                    ? EstadoItemEtapa.Concluida
                    : EstadoItemEtapa.Pendente))
            .ToArray();

        return new EstadoFluxoOperacional(
            atual,
            itens,
            atual is not EtapaFluxoOperacional.Concluido and not EtapaFluxoOperacional.Falha);
    }
}

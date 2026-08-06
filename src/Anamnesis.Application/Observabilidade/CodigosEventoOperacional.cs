namespace Anamnesis.Application.Observabilidade;

public static class CodigosEventoOperacional
{
    public const string GravacaoIniciada = "gravacao.iniciada";
    public const string GravacaoFinalizada = "gravacao.finalizada";
    public const string JobEnfileirado = "job.enfileirado";
    public const string JobReservado = "job.reservado";
    public const string TranscricaoIniciada = "transcricao.iniciada";
    public const string TranscricaoConcluida = "transcricao.concluida";
    public const string AtaGerada = "ata.gerada";
    public const string ReuniaoArquivada = "reuniao.arquivada";
    public const string JobConcluido = "job.concluido";
    public const string RetencaoAvaliada = "retencao.avaliada";
    public const string RetencaoAplicada = "retencao.aplicada";
    public const string OperacaoFalhou = "operacao.falhou";
    public const string DeteccaoDecidida = "deteccao.decidida";
    public const string WorkerExclusividadeExpirada = "worker.exclusividade_expirada";

    public static IReadOnlyList<string> Todos { get; } =
    [
        GravacaoIniciada,
        GravacaoFinalizada,
        JobEnfileirado,
        JobReservado,
        TranscricaoIniciada,
        TranscricaoConcluida,
        AtaGerada,
        ReuniaoArquivada,
        JobConcluido,
        RetencaoAvaliada,
        RetencaoAplicada,
        OperacaoFalhou,
        DeteccaoDecidida,
        WorkerExclusividadeExpirada
    ];
}

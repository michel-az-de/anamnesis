using Anamnesis.Domain.Tipos;

namespace Anamnesis.Domain.Entidades;

public sealed class Reuniao
{
    public Reuniao(Guid id, string titulo, DateTimeOffset criadaEm)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            throw new ArgumentException("O título da reunião é obrigatório.", nameof(titulo));
        }

        Id = id;
        Titulo = titulo.Trim();
        CriadaEm = criadaEm;
    }

    public Guid Id { get; }
    public string Titulo { get; }
    public DateTimeOffset CriadaEm { get; }
    public StatusReuniao Status { get; private set; } = StatusReuniao.Agendada;
    public Gravacao? Gravacao { get; private set; }
    public Transcricao? Transcricao { get; private set; }
    public Ata? Ata { get; private set; }
    public string? MotivoFalha { get; private set; }
    public DateTimeOffset? ArquivadaEm { get; private set; }

    public static Reuniao Reconstituir(
        Guid id,
        string titulo,
        DateTimeOffset criadaEm,
        StatusReuniao status,
        Gravacao? gravacao,
        Transcricao? transcricao,
        Ata? ata,
        string? motivoFalha,
        DateTimeOffset? arquivadaEm = null)
    {
        return new Reuniao(id, titulo, criadaEm)
        {
            Status = status,
            Gravacao = gravacao,
            Transcricao = transcricao,
            Ata = ata,
            MotivoFalha = motivoFalha,
            ArquivadaEm = arquivadaEm
        };
    }

    public void IniciarGravacao(DateTimeOffset iniciadaEm)
    {
        ExigirStatus(StatusReuniao.Agendada);
        Status = StatusReuniao.Gravando;
        Gravacao = new Gravacao(string.Empty, iniciadaEm, iniciadaEm);
    }

    public void FinalizarGravacao(string caminhoArquivo, DateTimeOffset finalizadaEm)
    {
        ExigirStatus(StatusReuniao.Gravando);

        if (string.IsNullOrWhiteSpace(caminhoArquivo))
        {
            throw new ArgumentException("O caminho da gravação é obrigatório.", nameof(caminhoArquivo));
        }

        Gravacao = new Gravacao(caminhoArquivo, Gravacao!.IniciadaEm, finalizadaEm);
        Status = StatusReuniao.AguardandoProcessamento;
    }

    public void IniciarTranscricao()
    {
        ExigirStatus(StatusReuniao.AguardandoProcessamento);
        Status = StatusReuniao.EmTranscricao;
    }

    public void RegistrarTranscricao(Transcricao transcricao)
    {
        ExigirStatus(StatusReuniao.EmTranscricao);
        Transcricao = transcricao;
        Status = StatusReuniao.EmAnalise;
    }

    public void RegistrarAta(Ata ata)
    {
        ExigirStatus(StatusReuniao.EmAnalise);
        Ata = ata;
        Status = StatusReuniao.AguardandoArquivamento;
    }

    public void MarcarArquivada(DateTimeOffset arquivadaEm)
    {
        ExigirStatus(StatusReuniao.AguardandoArquivamento);
        Status = StatusReuniao.Arquivada;
        ArquivadaEm = arquivadaEm;
    }

    public void MarcarArquivada() => MarcarArquivada(DateTimeOffset.UtcNow);

    public void MarcarPendenteExclusao()
    {
        ExigirStatus(StatusReuniao.Arquivada);
        Status = StatusReuniao.PendenteExclusao;
    }

    public void MarcarExcluida()
    {
        ExigirStatus(StatusReuniao.PendenteExclusao);
        Status = StatusReuniao.Excluida;
    }

    public void CancelarExclusaoPendente()
    {
        ExigirStatus(StatusReuniao.PendenteExclusao);
        Status = StatusReuniao.Arquivada;
    }

    public void RegistrarFalha(string motivo)
    {
        MotivoFalha = string.IsNullOrWhiteSpace(motivo) ? "Falha não especificada." : motivo;
        Status = StatusReuniao.Falha;
    }

    public void ReiniciarProcessamento()
    {
        ExigirStatus(StatusReuniao.Falha);
        Transcricao = null;
        Ata = null;
        MotivoFalha = null;
        ArquivadaEm = null;
        Status = StatusReuniao.AguardandoProcessamento;
    }

    private void ExigirStatus(StatusReuniao esperado)
    {
        if (Status != esperado)
        {
            throw new InvalidOperationException(
                $"A reunião está em '{Status}' e não pode executar esta operação. Estado esperado: '{esperado}'.");
        }
    }
}

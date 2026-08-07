using Anamnesis.Domain.Tipos;

namespace Anamnesis.Domain.Entidades;

public sealed class LembreteTarefa
{
    private LembreteTarefa(
        Guid id,
        Guid reuniaoId,
        string descricaoTarefa,
        DateTimeOffset lembrarEm,
        DateTimeOffset criadoEm,
        StatusLembreteTarefa status,
        DateTimeOffset? notificadoEm)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador do lembrete e obrigatorio.", nameof(id));
        }

        if (reuniaoId == Guid.Empty)
        {
            throw new ArgumentException("O identificador da reuniao e obrigatorio.", nameof(reuniaoId));
        }

        if (string.IsNullOrWhiteSpace(descricaoTarefa))
        {
            throw new ArgumentException("A descricao da tarefa e obrigatoria.", nameof(descricaoTarefa));
        }

        Id = id;
        ReuniaoId = reuniaoId;
        DescricaoTarefa = descricaoTarefa.Trim();
        LembrarEm = lembrarEm;
        CriadoEm = criadoEm;
        Status = status;
        NotificadoEm = notificadoEm;
    }

    public Guid Id { get; }
    public Guid ReuniaoId { get; }
    public string DescricaoTarefa { get; }
    public DateTimeOffset LembrarEm { get; }
    public DateTimeOffset CriadoEm { get; }
    public StatusLembreteTarefa Status { get; private set; }
    public DateTimeOffset? NotificadoEm { get; private set; }

    public static LembreteTarefa Criar(
        Guid id,
        Guid reuniaoId,
        string descricaoTarefa,
        DateTimeOffset lembrarEm,
        DateTimeOffset criadoEm) =>
        new(
            id,
            reuniaoId,
            descricaoTarefa,
            lembrarEm,
            criadoEm,
            StatusLembreteTarefa.Pendente,
            null);

    public static LembreteTarefa Reconstituir(
        Guid id,
        Guid reuniaoId,
        string descricaoTarefa,
        DateTimeOffset lembrarEm,
        DateTimeOffset criadoEm,
        StatusLembreteTarefa status,
        DateTimeOffset? notificadoEm) =>
        new(id, reuniaoId, descricaoTarefa, lembrarEm, criadoEm, status, notificadoEm);

    public void MarcarNotificado(DateTimeOffset notificadoEm)
    {
        if (Status != StatusLembreteTarefa.Pendente)
        {
            throw new InvalidOperationException("O lembrete ja foi notificado.");
        }

        if (notificadoEm < LembrarEm)
        {
            throw new InvalidOperationException("O lembrete ainda nao atingiu o horario configurado.");
        }

        Status = StatusLembreteTarefa.Notificado;
        NotificadoEm = notificadoEm;
    }
}

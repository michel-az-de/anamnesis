namespace Anamnesis.Tray;

public enum EtapaDesktopPoc
{
    Pronto,
    Gravando,
    Processando,
    Concluido
}

public sealed class ReuniaoDesktopPoc
{
    public required Guid Id { get; init; }

    public required string Titulo { get; init; }

    public required string Data { get; init; }

    public required string Plataforma { get; init; }

    public required string Duracao { get; init; }

    public required string Status { get; set; }

    public required string Resumo { get; init; }

    public required IReadOnlyList<string> PontosPrincipais { get; init; }

    public required IReadOnlyList<string> Transcricao { get; init; }

    public required IReadOnlyList<string> Decisoes { get; init; }

    public required IReadOnlyList<string> Tarefas { get; init; }

    public string? MotivoFalha { get; init; }

    public string? CaminhoAta { get; init; }

    public string? CaminhoTranscricao { get; init; }

    public string? CaminhoGravacao { get; init; }

    public string? DiretorioArtefatos { get; init; }
}

public sealed class DesktopPocState
{
    private readonly List<ReuniaoDesktopPoc> _reunioes = CriarHistorico();
    private ReuniaoDesktopPoc? _reuniaoProcessando;

    public EtapaDesktopPoc Etapa { get; private set; } = EtapaDesktopPoc.Pronto;

    public TimeSpan DuracaoGravacao { get; private set; }

    public IReadOnlyList<ReuniaoDesktopPoc> Reunioes => _reunioes;

    public void IniciarGravacao()
    {
        DuracaoGravacao = TimeSpan.Zero;
        Etapa = EtapaDesktopPoc.Gravando;
    }

    public void AvancarGravacao()
    {
        if (Etapa == EtapaDesktopPoc.Gravando)
        {
            DuracaoGravacao = DuracaoGravacao.Add(TimeSpan.FromSeconds(1));
        }
    }

    public ReuniaoDesktopPoc EncerrarGravacao()
    {
        if (Etapa != EtapaDesktopPoc.Gravando)
        {
            throw new InvalidOperationException("Não existe gravação simulada em andamento.");
        }

        _reuniaoProcessando = new ReuniaoDesktopPoc
        {
            Id = Guid.NewGuid(),
            Titulo = "Reunião sem título",
            Data = "Agora",
            Plataforma = "Google Meet",
            Duracao = FormatarDuracao(DuracaoGravacao),
            Status = "Transcrevendo",
            Resumo = "A gravação simulada foi concluída e entrou no fluxo local de processamento.",
            PontosPrincipais =
            [
                "Áudio do sistema e microfone foram representados na simulação.",
                "O próximo estado esperado é a geração da ata estruturada.",
                "Nenhuma integração externa foi acionada nesta POC."
            ],
            Transcricao =
            [
                "00:00:02  Esta é uma transcrição simulada para validar a experiência.",
                "00:00:08  O fluxo real usará o Whisper local após a gravação."
            ],
            Decisoes = ["Validar a experiência desktop antes de conectar os dados reais."],
            Tarefas = ["Felipe: revisar navegação, aparência e clareza dos estados."]
        };
        _reunioes.Insert(0, _reuniaoProcessando);
        Etapa = EtapaDesktopPoc.Processando;
        return _reuniaoProcessando;
    }

    public void ConcluirProcessamento()
    {
        if (_reuniaoProcessando is null || Etapa != EtapaDesktopPoc.Processando)
        {
            return;
        }

        _reuniaoProcessando.Status = "Ata pronta";
        Etapa = EtapaDesktopPoc.Concluido;
    }

    private static string FormatarDuracao(TimeSpan duracao) =>
        duracao.TotalMinutes >= 1
            ? $"{(int)duracao.TotalMinutes}:{duracao.Seconds:00} min"
            : $"0:{duracao.Seconds:00} min";

    private static List<ReuniaoDesktopPoc> CriarHistorico() =>
    [
        CriarReuniao(
            "Planejamento do produto",
            "Hoje, 14:00",
            "Google Meet",
            "42 min",
            "A equipe definiu a experiência visual do Anamnesis e priorizou uma interface Windows moderna, com histórico pesquisável e acesso direto às atas."),
        CriarReuniao(
            "Revisão da arquitetura",
            "Hoje, 10:30",
            "Microsoft Teams",
            "31 min",
            "Foram revisadas as fronteiras entre Tray, Worker e integrações locais."),
        CriarReuniao(
            "Alinhamento semanal",
            "Ontem, 16:00",
            "Zoom",
            "54 min",
            "A equipe alinhou entregas, riscos e próximos experimentos do produto.",
            "Transcrevendo"),
        CriarReuniao(
            "Entrevista com usuário",
            "04 ago, 09:00",
            "Google Meet",
            "38 min",
            "O usuário reforçou a importância de encontrar reuniões e decisões sem usar o terminal.")
    ];

    private static ReuniaoDesktopPoc CriarReuniao(
        string titulo,
        string data,
        string plataforma,
        string duracao,
        string resumo,
        string status = "Ata pronta") =>
        new()
        {
            Id = Guid.NewGuid(),
            Titulo = titulo,
            Data = data,
            Plataforma = plataforma,
            Duracao = duracao,
            Status = status,
            Resumo = resumo,
            PontosPrincipais =
            [
                "Construir uma experiência desktop clara e silenciosa.",
                "Manter Tray e Worker como componentes operacionais.",
                "Exibir evidências e estados sem depender do terminal."
            ],
            Transcricao =
            [
                "00:00:08  Vamos planejar uma interface visual Windows bem legal.",
                "00:03:41  Precisamos acessar o histórico, os textos e as configurações.",
                "00:11:20  A primeira versão deve validar navegação e comportamento."
            ],
            Decisoes =
            [
                "Validar primeiro uma POC nativa do Windows.",
                "Manter a POC sem dependência de integrações reais."
            ],
            Tarefas =
            [
                "Felipe: validar a POC visual.",
                "Anamnesis: conectar consultas reais em uma próxima SPEK."
            ]
        };
}

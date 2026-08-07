using Anamnesis.Application.Observabilidade;

namespace Anamnesis.Tray;

internal enum EstadoDiagnosticoGuiado
{
    Pronto,
    Capturando,
    Processando,
    Sucesso,
    Falha,
    Cancelado
}

internal sealed class DiagnosticoGuiadoDesktop
{
    private string _componente = "Desktop";
    private string _etapa = "Preparando";
    private string _mensagemOriginal = "Teste ainda não iniciado.";

    public EstadoDiagnosticoGuiado Estado { get; private set; } = EstadoDiagnosticoGuiado.Pronto;

    public Guid? ReuniaoId { get; private set; }

    public string Mensagem { get; private set; } = "Fale normalmente por cinco segundos para validar o pipeline local.";

    public string? TrechoReconhecido { get; private set; }

    public bool TudoCerto => Estado == EstadoDiagnosticoGuiado.Sucesso;

    public void Iniciar(Guid reuniaoId)
    {
        ReuniaoId = reuniaoId;
        Estado = EstadoDiagnosticoGuiado.Capturando;
        Mensagem = "Capturando sua voz por cinco segundos.";
        TrechoReconhecido = null;
        _componente = "OBS";
        _etapa = "Gravando";
        _mensagemOriginal = "Captura do teste em andamento.";
    }

    public void MarcarProcessando()
    {
        Estado = EstadoDiagnosticoGuiado.Processando;
        Mensagem = "Gravação salva. Aguardando transcrição local.";
        _componente = "Worker";
        _etapa = "Transcrevendo";
        _mensagemOriginal = "Processamento do teste em andamento.";
    }

    public void Concluir(IReadOnlyList<string> transcricao)
    {
        var trecho = transcricao.FirstOrDefault(linha => !string.IsNullOrWhiteSpace(linha))?.Trim();
        if (string.IsNullOrWhiteSpace(trecho))
        {
            Falhar(
                "Whisper",
                "Transcrevendo",
                "O teste terminou, mas nenhum texto foi reconhecido.");
            return;
        }

        Estado = EstadoDiagnosticoGuiado.Sucesso;
        Mensagem = "Tudo certo. Áudio gravado e transcrição reconhecida.";
        TrechoReconhecido = trecho.Length <= 220 ? trecho : trecho[..220];
        _componente = "Pipeline";
        _etapa = "Concluído";
        _mensagemOriginal = "Teste guiado concluído com sucesso.";
    }

    public void Falhar(string componente, string etapa, string mensagem)
    {
        Estado = EstadoDiagnosticoGuiado.Falha;
        _componente = componente;
        _etapa = etapa;
        _mensagemOriginal = mensagem;
        Mensagem = RedatorMensagemOperacional.Redigir(mensagem);
        TrechoReconhecido = null;
    }

    public void Cancelar()
    {
        Estado = EstadoDiagnosticoGuiado.Cancelado;
        Mensagem = "Teste cancelado. A captura foi encerrada com segurança e seguirá o processamento normal.";
        _componente = "Desktop";
        _etapa = "Cancelado";
        _mensagemOriginal = Mensagem;
    }

    public string CriarDiagnosticoCopiavel()
    {
        var correlacao = ReuniaoId is null ? "r:indisponivel" : $"r:{ReuniaoId:N}";
        return string.Join(
            Environment.NewLine,
            "Anamnesis | diagnóstico guiado",
            $"resultado={Estado.ToString().ToLowerInvariant()}",
            $"etapa={RedatorMensagemOperacional.Redigir(_etapa)}",
            $"componente={RedatorMensagemOperacional.Redigir(_componente)}",
            $"correlacao={correlacao}",
            $"mensagem={RedatorMensagemOperacional.Redigir(_mensagemOriginal)}");
    }
}

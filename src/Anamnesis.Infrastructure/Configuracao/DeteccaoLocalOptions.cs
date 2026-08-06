using System.Text.Json.Serialization;
using Anamnesis.Application.Modelos;

namespace Anamnesis.Infrastructure.Configuracao;

public sealed record AplicativoDeteccaoOptions(
    string Chave,
    string Nome,
    IReadOnlyList<string> FamiliasProcesso);

public sealed record JanelaChamadaOptions(
    string Chave,
    string Nome,
    IReadOnlyList<string> Assinaturas);

public sealed record DeteccaoLocalOptions
{
    [JsonConverter(typeof(JsonStringEnumConverter<ModoDeteccaoReuniao>))]
    public ModoDeteccaoReuniao Modo { get; init; } = ModoDeteccaoReuniao.Assistido;

    public bool FinalizacaoAutomaticaAtiva { get; init; }

    public int CooldownMinutos { get; init; } = 10;

    public int SilencioMinutos { get; init; } = 60;

    public IReadOnlyList<AplicativoDeteccaoOptions> Aplicativos { get; init; } =
    [
        new("native_teams", "Microsoft Teams", ["ms-teams", "teams"]),
        new("native_zoom", "Zoom", ["zoom"]),
        new("native_discord", "Discord", ["discord"])
    ];

    public IReadOnlyList<string> Navegadores { get; init; } =
        ["chrome", "msedge", "firefox", "brave"];

    public IReadOnlyList<JanelaChamadaOptions> JanelasChamada { get; init; } =
    [
        new("browser_meet", "Google Meet", ["Google Meet", "Meet -"]),
        new("browser_teams", "Microsoft Teams", ["Microsoft Teams"]),
        new("browser_zoom", "Zoom", ["Zoom Meeting"]),
        new("browser_discord", "Discord", ["Discord"])
    ];

    public static DeteccaoLocalOptions Padrao { get; } = new();

    public PoliticaDeteccaoOptions CriarPoliticaOptions() =>
        PoliticaDeteccaoOptions.Padrao with
        {
            Modo = Modo,
            Cooldown = TimeSpan.FromMinutes(Math.Clamp(CooldownMinutos, 1, 1_440)),
            DuracaoSilencio = TimeSpan.FromMinutes(Math.Clamp(SilencioMinutos, 1, 10_080)),
            FinalizacaoAutomaticaAtiva = FinalizacaoAutomaticaAtiva
        };
}

namespace Anamnesis.Tray;

public sealed record DesktopPocSpacingTokens(IReadOnlyList<int> Escala)
{
    public int Xs => Escala[0];

    public int Sm => Escala[1];

    public int Md => Escala[2];

    public int Lg => Escala[3];

    public int Xl => Escala[4];

    public int Xxl => Escala[5];
}

public sealed record DesktopPocGeometryTokens(
    IReadOnlyList<int> Raios,
    int Borda,
    int BordaFoco)
{
    public int RaioPequeno => Raios[0];

    public int RaioMedio => Raios[1];

    public int RaioGrande => Raios[2];

    public int RaioHeroi => Raios[3];
}

public sealed record DesktopPocMotionTokens(
    int FasterMs,
    int FastMs,
    int NormalMs,
    int DeslocamentoPagina);

public sealed record DesktopPocTypographyTokens(
    string Interface,
    string Display,
    string Mono,
    float Corpo,
    float Titulo,
    float Dado);

public sealed record DesktopPocDesignTokens(
    DesktopPocSpacingTokens Espacamento,
    DesktopPocGeometryTokens Geometria,
    DesktopPocMotionTokens Motion,
    DesktopPocTypographyTokens Tipografia)
{
    public static DesktopPocDesignTokens Padrao { get; } = new(
        new DesktopPocSpacingTokens([4, 8, 12, 16, 24, 32]),
        new DesktopPocGeometryTokens([6, 8, 12, 16], 1, 2),
        new DesktopPocMotionTokens(83, 167, 250, 60),
        new DesktopPocTypographyTokens(
            ResolverFonte("Segoe UI Variable Text", "Segoe UI"),
            ResolverFonte("Segoe UI Variable Display", "Segoe UI"),
            ResolverFonte("Cascadia Mono", "Consolas"),
            10F,
            21F,
            9F));

    private static string ResolverFonte(string preferida, string fallback)
    {
        using var fonte = new Font(preferida, 10F, FontStyle.Regular, GraphicsUnit.Point);
        return string.Equals(fonte.Name, preferida, StringComparison.OrdinalIgnoreCase)
            ? preferida
            : fallback;
    }
}

public sealed record DesktopPocSurfacePalette(
    Color Canvas,
    Color Painel,
    Color PainelElevado,
    Color PainelHover,
    Color BordaForte,
    Color DestaqueSuave,
    Color PositivoSuave,
    Color Sombra);

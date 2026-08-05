namespace Anamnesis.Tray;

public enum TemaDesktopPoc
{
    Claro,
    Escuro
}

public sealed record DesktopPocPalette(
    Color Fundo,
    Color Superficie,
    Color Navegacao,
    Color Texto,
    Color TextoSecundario,
    Color TextoNavegacao,
    Color Borda,
    Color Selecao,
    Color Destaque,
    Color TextoSobreDestaque,
    Color Positivo,
    Color Perigo,
    Color FundoDestaque,
    Color FundoPositivo,
    Color ConsoleFundo,
    Color ConsoleLinha,
    Color ConsoleTexto,
    Color ConsoleSecundario,
    DesktopPocSurfacePalette Superficies)
{
    public static DesktopPocPalette Criar(TemaDesktopPoc tema) => tema == TemaDesktopPoc.Escuro
        ? new DesktopPocPalette(
            Fundo: Color.FromArgb(12, 17, 24),
            Superficie: Color.FromArgb(20, 27, 36),
            Navegacao: Color.FromArgb(14, 20, 28),
            Texto: Color.FromArgb(242, 244, 247),
            TextoSecundario: Color.FromArgb(150, 161, 177),
            TextoNavegacao: Color.FromArgb(224, 229, 235),
            Borda: Color.FromArgb(39, 49, 62),
            Selecao: Color.FromArgb(29, 39, 51),
            Destaque: Color.FromArgb(212, 154, 91),
            TextoSobreDestaque: Color.FromArgb(19, 15, 10),
            Positivo: Color.FromArgb(76, 183, 154),
            Perigo: Color.FromArgb(227, 109, 120),
            FundoDestaque: Color.FromArgb(48, 36, 25),
            FundoPositivo: Color.FromArgb(20, 44, 38),
            ConsoleFundo: Color.FromArgb(9, 13, 18),
            ConsoleLinha: Color.FromArgb(17, 24, 33),
            ConsoleTexto: Color.FromArgb(214, 222, 232),
            ConsoleSecundario: Color.FromArgb(132, 146, 164),
            Superficies: new DesktopPocSurfacePalette(
                Canvas: Color.FromArgb(12, 17, 24),
                Painel: Color.FromArgb(20, 27, 36),
                PainelElevado: Color.FromArgb(24, 33, 44),
                PainelHover: Color.FromArgb(31, 42, 55),
                BordaForte: Color.FromArgb(59, 72, 88),
                DestaqueSuave: Color.FromArgb(48, 36, 25),
                PositivoSuave: Color.FromArgb(20, 44, 38),
                Sombra: Color.FromArgb(5, 8, 12)))
        : new DesktopPocPalette(
            Fundo: Color.FromArgb(243, 245, 248),
            Superficie: Color.White,
            Navegacao: Color.FromArgb(16, 23, 46),
            Texto: Color.FromArgb(16, 23, 46),
            TextoSecundario: Color.FromArgb(96, 106, 125),
            TextoNavegacao: Color.FromArgb(243, 238, 228),
            Borda: Color.FromArgb(222, 226, 235),
            Selecao: Color.FromArgb(43, 54, 86),
            Destaque: Color.FromArgb(184, 115, 51),
            TextoSobreDestaque: Color.White,
            Positivo: Color.FromArgb(77, 139, 122),
            Perigo: Color.FromArgb(185, 48, 61),
            FundoDestaque: Color.FromArgb(249, 240, 231),
            FundoPositivo: Color.FromArgb(236, 246, 242),
            ConsoleFundo: Color.FromArgb(13, 18, 29),
            ConsoleLinha: Color.FromArgb(24, 31, 47),
            ConsoleTexto: Color.FromArgb(224, 230, 240),
            ConsoleSecundario: Color.FromArgb(148, 162, 186),
            Superficies: new DesktopPocSurfacePalette(
                Canvas: Color.FromArgb(243, 245, 248),
                Painel: Color.White,
                PainelElevado: Color.FromArgb(248, 249, 251),
                PainelHover: Color.FromArgb(237, 240, 245),
                BordaForte: Color.FromArgb(181, 190, 203),
                DestaqueSuave: Color.FromArgb(249, 240, 231),
                PositivoSuave: Color.FromArgb(236, 246, 242),
                Sombra: Color.FromArgb(205, 211, 221)));
}

public static class DesktopPocTheme
{
    public static void HerdarDoWindows() =>
        System.Windows.Forms.Application.SetColorMode(SystemColorMode.System);

    public static TemaDesktopPoc ObterAtual() =>
        Interpretar(System.Windows.Forms.Application.SystemColorMode);

    public static TemaDesktopPoc Interpretar(SystemColorMode modoWindows) =>
        modoWindows == SystemColorMode.Dark ? TemaDesktopPoc.Escuro : TemaDesktopPoc.Claro;
}

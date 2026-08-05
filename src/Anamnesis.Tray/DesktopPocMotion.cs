using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Anamnesis.Tray;

public sealed record DesktopPocEffectsPolicy(bool AnimacoesAtivas);

public static class DesktopPocSystemPreferences
{
    private const uint ObterAnimacoesAreaCliente = 0x1042;

    public static DesktopPocEffectsPolicy Obter() =>
        Interpretar(LerPreferencia(ObterAnimacoesAreaCliente, padrao: true));

    public static DesktopPocEffectsPolicy Interpretar(bool animacoesAreaCliente) =>
        new(animacoesAreaCliente);

    private static bool LerPreferencia(uint acao, bool padrao)
    {
        var valor = padrao ? 1 : 0;
        return SystemParametersInfo(acao, 0, ref valor, 0) ? valor != 0 : padrao;
    }

    [SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time",
        Justification = "As preferências Win32 são pequenas e evitam habilitar blocos unsafe no projeto WinForms.")]
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint acao,
        uint parametro,
        ref int valor,
        uint atualizacao);
}

public static class DesktopPocMotion
{
    public static double SuavizarSaida(double progresso)
    {
        var limitado = Math.Clamp(progresso, 0D, 1D);
        return 1D - Math.Pow(1D - limitado, 3D);
    }

    public static int CalcularDeslocamento(double progresso, int deslocamento)
    {
        var restante = 1D - SuavizarSaida(progresso);
        return (int)Math.Round(deslocamento * restante, MidpointRounding.AwayFromZero);
    }

    public static Color Misturar(Color origem, Color destino, double progresso)
    {
        var limitado = Math.Clamp(progresso, 0D, 1D);
        return Color.FromArgb(
            Interpolar(origem.A, destino.A, limitado),
            Interpolar(origem.R, destino.R, limitado),
            Interpolar(origem.G, destino.G, limitado),
            Interpolar(origem.B, destino.B, limitado));
    }

    public static void AnimarEntrada(
        Control controle,
        Rectangle destino,
        DesktopPocMotionTokens tokens,
        bool animacoesAtivas)
    {
        if (!animacoesAtivas || destino.Width <= 0 || destino.Height <= 0)
        {
            controle.Dock = DockStyle.Fill;
            return;
        }

        controle.Dock = DockStyle.None;
        controle.Bounds = new Rectangle(
            destino.X + tokens.DeslocamentoPagina,
            destino.Y,
            destino.Width,
            destino.Height);

        var relogio = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        void Encerrar()
        {
            timer.Stop();
            timer.Dispose();
            if (!controle.IsDisposed)
            {
                controle.Bounds = destino;
                controle.Dock = DockStyle.Fill;
            }
        }

        timer.Tick += (_, _) =>
        {
            if (controle.IsDisposed)
            {
                Encerrar();
                return;
            }

            var progresso = relogio.Elapsed.TotalMilliseconds / tokens.NormalMs;
            if (progresso >= 1D)
            {
                Encerrar();
                return;
            }

            controle.Left = destino.X + CalcularDeslocamento(progresso, tokens.DeslocamentoPagina);
        };
        controle.Disposed += (_, _) =>
        {
            if (timer.Enabled)
            {
                timer.Stop();
                timer.Dispose();
            }
        };
        timer.Start();
    }

    private static int Interpolar(int origem, int destino, double progresso) =>
        (int)Math.Round(origem + ((destino - origem) * progresso), MidpointRounding.AwayFromZero);
}

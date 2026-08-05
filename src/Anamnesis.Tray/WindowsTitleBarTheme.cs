using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Anamnesis.Tray;

internal static class WindowsTitleBarTheme
{
    private const int UsarModoEscuroImersivo = 20;
    private const int PreferenciaCantos = 33;
    private const int CorDaBorda = 34;
    private const int CorDaLegenda = 35;
    private const int CorDoTexto = 36;
    private const int TipoBackdrop = 38;
    private const int CantosArredondados = 2;
    private const int BackdropNenhum = 1;

    public static void Aplicar(Form form, TemaDesktopPoc tema) =>
        Aplicar(form, tema, DesktopPocPalette.Criar(tema));

    public static void Aplicar(Form form, TemaDesktopPoc tema, DesktopPocPalette paleta)
    {
        var habilitado = tema == TemaDesktopPoc.Escuro ? 1 : 0;
        Definir(form.Handle, UsarModoEscuroImersivo, habilitado);

        if (Environment.OSVersion.Version >= new Version(10, 0, 22000))
        {
            Definir(form.Handle, PreferenciaCantos, CantosArredondados);
            Definir(form.Handle, CorDaBorda, ConverterColorRef(paleta.Borda));
            Definir(form.Handle, CorDaLegenda, ConverterColorRef(paleta.Navegacao));
            Definir(form.Handle, CorDoTexto, ConverterColorRef(paleta.TextoNavegacao));
        }

        if (Environment.OSVersion.Version >= new Version(10, 0, 22621))
        {
            Definir(form.Handle, TipoBackdrop, ObterTipoBackdrop(Environment.OSVersion.Version));
        }
    }

    internal static int ObterTipoBackdrop(Version _) => BackdropNenhum;

    internal static int ConverterColorRef(Color cor) =>
        cor.R | (cor.G << 8) | (cor.B << 16);

    private static void Definir(nint janela, int atributo, int valor) =>
        _ = DwmSetWindowAttribute(janela, atributo, ref valor, Marshal.SizeOf<int>());

    [SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time",
        Justification = "A chamada pequena e exclusiva do Windows evita habilitar blocos unsafe no projeto WinForms.")]
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        nint janela,
        int atributo,
        ref int valor,
        int tamanhoValor);
}

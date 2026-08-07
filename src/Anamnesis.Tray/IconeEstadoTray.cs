using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Anamnesis.Tray;

internal static class IconeEstadoTray
{
    public static Icon CriarGravando(Icon iconeBase)
    {
        ArgumentNullException.ThrowIfNull(iconeBase);
        using var imagem = new Bitmap(32, 32);
        using (var desenho = Graphics.FromImage(imagem))
        {
            desenho.SmoothingMode = SmoothingMode.AntiAlias;
            desenho.DrawIcon(iconeBase, new Rectangle(0, 0, 32, 32));
            using var contorno = new SolidBrush(Color.White);
            using var gravando = new SolidBrush(Color.FromArgb(211, 47, 62));
            desenho.FillEllipse(contorno, 19, 19, 13, 13);
            desenho.FillEllipse(gravando, 21, 21, 9, 9);
        }

        var handle = imagem.GetHicon();
        try
        {
            using var temporario = Icon.FromHandle(handle);
            return (Icon)temporario.Clone();
        }
        finally
        {
            _ = DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}

namespace Anamnesis.Tray;

internal static class TituloReuniaoManual
{
    internal const string Padrao = "Reunião sem título";

    internal static string Normalizar(string? titulo) =>
        string.IsNullOrWhiteSpace(titulo) ? Padrao : titulo.Trim();
}

internal sealed class DialogoTituloReuniao : Form
{
    private readonly DesktopTextField _titulo;

    private DialogoTituloReuniao(
        TemaDesktopPoc tema,
        DesktopPocEffectsPolicy politicaVisual)
    {
        var paleta = DesktopPocPalette.Criar(tema);
        var tokens = DesktopPocDesignTokens.Padrao;
        Text = "Título da reunião";
        Icon = IconeAnamnesis.Carregar();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(480, 220);
        BackColor = paleta.Superficies.Canvas;
        ForeColor = paleta.Texto;
        Font = new Font(tokens.Tipografia.Interface, tokens.Tipografia.Corpo, FontStyle.Regular, GraphicsUnit.Point);

        var superficie = new DesktopSurfacePanel(
            paleta,
            tokens,
            politicaVisual,
            DesktopSurfaceVariant.Elevated)
        {
            Dock = DockStyle.Fill,
            CornerRadius = 0,
            Padding = new Padding(24)
        };
        var titulo = new Label
        {
            Text = "Como você quer chamar esta reunião?",
            AutoSize = true,
            ForeColor = paleta.Texto,
            Font = new Font(tokens.Tipografia.Display, 13F, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(24, 24)
        };
        var ajuda = new Label
        {
            Text = "Você pode deixar o título padrão e editar depois.",
            AutoSize = true,
            ForeColor = paleta.TextoSecundario,
            Location = new Point(26, 58)
        };
        _titulo = new DesktopTextField(paleta, tokens, "Ex.: Planejamento semanal")
        {
            Text = TituloReuniaoManual.Padrao,
            Location = new Point(24, 88),
            Width = 432
        };
        var cancelar = CriarBotao(paleta, tokens, politicaVisual, "Cancelar", DesktopActionVariant.Secondary);
        cancelar.DialogResult = DialogResult.Cancel;
        cancelar.Location = new Point(184, 148);
        var iniciar = CriarBotao(paleta, tokens, politicaVisual, "Iniciar gravação", DesktopActionVariant.Primary);
        iniciar.DialogResult = DialogResult.OK;
        iniciar.Location = new Point(316, 148);

        superficie.Controls.Add(iniciar);
        superficie.Controls.Add(cancelar);
        superficie.Controls.Add(_titulo);
        superficie.Controls.Add(ajuda);
        superficie.Controls.Add(titulo);
        Controls.Add(superficie);
        AcceptButton = iniciar;
        CancelButton = cancelar;
        Shown += (_, _) =>
        {
            WindowsTitleBarTheme.Aplicar(this, tema, paleta);
            _titulo.Focus();
        };
    }

    internal static string? Solicitar(
        IWin32Window? dono,
        TemaDesktopPoc tema,
        DesktopPocEffectsPolicy politicaVisual)
    {
        using var dialogo = new DialogoTituloReuniao(tema, politicaVisual);
        var resultado = dono is null ? dialogo.ShowDialog() : dialogo.ShowDialog(dono);
        return resultado == DialogResult.OK
            ? TituloReuniaoManual.Normalizar(dialogo._titulo.Text)
            : null;
    }

    private static DesktopActionButton CriarBotao(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        DesktopPocEffectsPolicy politicaVisual,
        string texto,
        DesktopActionVariant variante) =>
        new(paleta, tokens, politicaVisual, variante)
        {
            Text = texto,
            Size = new Size(132, 40),
            Font = new Font(tokens.Tipografia.Interface, 9.5F, FontStyle.Bold, GraphicsUnit.Point)
        };
}

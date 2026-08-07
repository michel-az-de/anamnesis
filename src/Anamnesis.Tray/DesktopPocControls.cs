using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Anamnesis.Tray;

internal enum DesktopSurfaceVariant
{
    Base,
    Elevated,
    Chrome,
    Navigation,
    Console
}

internal enum DesktopActionVariant
{
    Primary,
    Secondary,
    Danger,
    Ghost
}

internal enum DesktopNavigationIcon
{
    None,
    Home,
    Meetings,
    Live,
    Tasks,
    Activity,
    Observability,
    Settings
}

internal sealed class DesktopBackdropPanel : Panel
{
    private readonly DesktopPocPalette _paleta;

    public DesktopBackdropPanel(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens _,
        DesktopPocEffectsPolicy politica)
    {
        _paleta = paleta;
        AnimacoesAtivas = politica.AnimacoesAtivas;
        DecoracaoAtiva = false;
        AnimacaoContinuaAtiva = false;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = paleta.Superficies.Canvas;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    public bool AnimacoesAtivas { get; }

    public bool DecoracaoAtiva { get; }

    public bool AnimacaoContinuaAtiva { get; }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(_paleta.Superficies.Canvas);
    }
}

internal sealed class DesktopSurfacePanel : Panel
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopInteractionAnimator _hover;
    private DesktopSurfaceVariant _variant;
    private int _cornerRadius;

    public DesktopSurfacePanel(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        DesktopPocEffectsPolicy politica,
        DesktopSurfaceVariant variant = DesktopSurfaceVariant.Base)
    {
        _paleta = paleta;
        _variant = variant;
        _cornerRadius = tokens.Geometria.RaioGrande;
        _hover = new DesktopInteractionAnimator(this, tokens.Motion, politica.AnimacoesAtivas);
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = CorDaSuperficie();
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DesktopSurfaceVariant Variant
    {
        get => _variant;
        set
        {
            _variant = value;
            BackColor = CorDaSuperficie();
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(0, value);
            AtualizarRegiao();
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Interactive { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? AccentColor { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (Interactive)
        {
            _hover.Definir(ativo: true);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (Interactive)
        {
            _hover.Definir(ativo: false);
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        AtualizarRegiao();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(area, CornerRadius);
        var fundo = CorDaSuperficie();
        if (Interactive && _hover.Valor > 0D)
        {
            fundo = DesktopPocMotion.Misturar(fundo, _paleta.Superficies.PainelHover, _hover.Valor);
        }

        using var material = new SolidBrush(fundo);
        e.Graphics.FillPath(material, caminho);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(area, CornerRadius);
        var destaque = AccentColor ?? _paleta.Borda;
        var intensidade = AccentColor is null ? 0D : 0.2D + (_hover.Valor * 0.35D);
        var borda = DesktopPocMotion.Misturar(_paleta.Borda, destaque, intensidade);
        using var caneta = new Pen(borda, Interactive && _hover.Valor > 0.45D ? 1.4F : 1F);
        e.Graphics.DrawPath(caneta, caminho);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hover.Dispose();
        }

        base.Dispose(disposing);
    }

    private Color CorDaSuperficie() => Variant switch
    {
        DesktopSurfaceVariant.Chrome => _paleta.Superficies.PainelElevado,
        DesktopSurfaceVariant.Navigation => _paleta.Navegacao,
        DesktopSurfaceVariant.Elevated => _paleta.Superficies.PainelElevado,
        DesktopSurfaceVariant.Console => _paleta.ConsoleFundo,
        _ => _paleta.Superficies.Painel
    };

    private void AtualizarRegiao()
    {
        Region?.Dispose();
        if (CornerRadius <= 0 || ClientSize.Width <= 1 || ClientSize.Height <= 1)
        {
            Region = null;
            return;
        }

        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(ClientRectangle, CornerRadius);
        Region = new Region(caminho);
    }
}

internal sealed class DesktopActionButton : Button
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;
    private readonly DesktopInteractionAnimator _hover;
    private bool _pressionado;

    public DesktopActionButton(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        DesktopPocEffectsPolicy politica,
        DesktopActionVariant variant)
    {
        _paleta = paleta;
        _tokens = tokens;
        Variant = variant;
        _hover = new DesktopInteractionAnimator(this, tokens.Motion, politica.AnimacoesAtivas);
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = paleta.Superficies.Painel;
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint,
            true);
    }

    public DesktopActionVariant Variant { get; }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var texto = TextRenderer.MeasureText(Text, Font);
        return new Size(Math.Max(MinimumSize.Width, texto.Width + 34), Math.Max(MinimumSize.Height, 40));
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover.Definir(ativo: true);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _pressionado = false;
        _hover.Definir(ativo: false);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        _pressionado = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _pressionado = false;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Region?.Dispose();
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(ClientRectangle, _tokens.Geometria.RaioMedio);
        Region = new Region(caminho);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(1, 1, Math.Max(1, ClientSize.Width - 3), Math.Max(1, ClientSize.Height - 3));
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(area, _tokens.Geometria.RaioMedio);
        var (fundo, texto, borda) = CoresDoBotao();
        var resposta = _pressionado ? 0.14D : _hover.Valor * 0.09D;
        fundo = DesktopPocMotion.Misturar(fundo, _pressionado ? Color.Black : Color.White, resposta);
        if (!Enabled)
        {
            fundo = DesktopPocMotion.Misturar(fundo, _paleta.Fundo, 0.55D);
            texto = _paleta.TextoSecundario;
        }

        using var preenchimento = new SolidBrush(fundo);
        pevent.Graphics.FillPath(preenchimento, caminho);
        using var caneta = new Pen(
            DesktopPocMotion.Misturar(borda, _paleta.Superficies.BordaForte, _hover.Valor * 0.45D),
            Focused ? _tokens.Geometria.BordaFoco : _tokens.Geometria.Borda);
        pevent.Graphics.DrawPath(caneta, caminho);

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            area,
            texto,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hover.Dispose();
            Region?.Dispose();
        }

        base.Dispose(disposing);
    }

    private (Color Fundo, Color Texto, Color Borda) CoresDoBotao() => Variant switch
    {
        DesktopActionVariant.Primary => (
            _paleta.Destaque,
            _paleta.TextoSobreDestaque,
            _paleta.Destaque),
        DesktopActionVariant.Danger => (
            _paleta.Perigo,
            _paleta.TextoSobreDestaque,
            _paleta.Perigo),
        DesktopActionVariant.Ghost => (
            _paleta.Superficies.Painel,
            _paleta.Texto,
            _paleta.Borda),
        _ => (
            _paleta.Superficies.PainelElevado,
            _paleta.Texto,
            _paleta.Borda)
    };
}

internal sealed class DesktopNavigationButton : Button
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;
    private readonly DesktopInteractionAnimator _hover;
    private bool _selecionado;

    public DesktopNavigationButton(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        DesktopPocEffectsPolicy politica,
        DesktopNavigationIcon icon)
    {
        _paleta = paleta;
        _tokens = tokens;
        Icon = icon;
        _hover = new DesktopInteractionAnimator(this, tokens.Motion, politica.AnimacoesAtivas);
        DoubleBuffered = true;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = paleta.Navegacao;
        ForeColor = paleta.TextoNavegacao;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.MenuItem;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint,
            true);
    }

    public bool Selecionado => _selecionado;

    public DesktopNavigationIcon Icon { get; }

    public void DefinirSelecionado(bool selecionado)
    {
        _selecionado = selecionado;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover.Definir(ativo: true);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover.Definir(ativo: false);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(_paleta.Navegacao);
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var escala = Math.Max(1F, DeviceDpi / 96F);
        var margemHorizontal = (int)Math.Round(6F * escala);
        var margemVertical = (int)Math.Round(3F * escala);
        var area = new Rectangle(
            margemHorizontal,
            margemVertical,
            Math.Max(1, ClientSize.Width - margemHorizontal * 2),
            Math.Max(1, ClientSize.Height - margemVertical * 2));

        if (_selecionado || _hover.Valor > 0.01D)
        {
            using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(
                area,
                _tokens.Geometria.RaioPequeno);
            var fundo = _selecionado
                ? DesktopPocMotion.Misturar(_paleta.Selecao, _paleta.Superficies.PainelHover, _hover.Valor)
                : DesktopPocMotion.Misturar(_paleta.Navegacao, _paleta.Selecao, _hover.Valor);
            using var pincel = new SolidBrush(fundo);
            pevent.Graphics.FillPath(pincel, caminho);
        }

        if (_selecionado)
        {
            using var marcador = new Pen(_paleta.Destaque, Math.Max(3F, 3F * escala))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            var x = area.Left + Math.Max(1F, escala);
            pevent.Graphics.DrawLine(
                marcador,
                x,
                area.Top + 9F * escala,
                x,
                area.Bottom - 9F * escala);
        }

        if (Focused)
        {
            var foco = Rectangle.Inflate(area, -(int)Math.Round(2F * escala), -(int)Math.Round(2F * escala));
            ControlPaint.DrawFocusRectangle(pevent.Graphics, foco, _paleta.TextoNavegacao, _paleta.Navegacao);
        }

        var corIcone = _selecionado ? _paleta.Destaque : _paleta.TextoSecundario;
        var tamanhoIcone = (int)Math.Round(20F * escala);
        var esquerdaIcone = (int)Math.Round(18F * escala);
        DesenharIcone(
            pevent.Graphics,
            new Rectangle(
                esquerdaIcone,
                (ClientSize.Height - tamanhoIcone) / 2,
                tamanhoIcone,
                tamanhoIcone),
            corIcone);

        var esquerdaTexto = (int)Math.Round(52F * escala);

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            new Rectangle(
                esquerdaTexto,
                0,
                Math.Max(1, ClientSize.Width - esquerdaTexto - (int)Math.Round(12F * escala)),
                ClientSize.Height),
            _paleta.TextoNavegacao,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    private void DesenharIcone(Graphics graphics, Rectangle area, Color cor)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var escalaX = area.Width / 20F;
        var escalaY = area.Height / 20F;
        PointF P(float x, float y) => new(area.Left + x * escalaX, area.Top + y * escalaY);
        float X(float x) => area.Left + x * escalaX;
        float Y(float y) => area.Top + y * escalaY;
        using var caneta = new Pen(cor, Math.Max(1.6F, 1.6F * Math.Min(escalaX, escalaY)))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        switch (Icon)
        {
            case DesktopNavigationIcon.Home:
                graphics.DrawLines(caneta, [P(3, 9), P(10, 3), P(17, 9)]);
                graphics.DrawRectangle(caneta, X(5), Y(9), 10 * escalaX, 8 * escalaY);
                break;
            case DesktopNavigationIcon.Meetings:
                graphics.DrawRectangle(caneta, X(3), Y(4), 14 * escalaX, 13 * escalaY);
                graphics.DrawLine(caneta, P(3, 8), P(17, 8));
                graphics.DrawLine(caneta, P(7, 2), P(7, 6));
                graphics.DrawLine(caneta, P(13, 2), P(13, 6));
                break;
            case DesktopNavigationIcon.Live:
                graphics.DrawEllipse(caneta, X(3), Y(3), 14 * escalaX, 14 * escalaY);
                using (var ponto = new SolidBrush(cor))
                {
                    graphics.FillEllipse(ponto, X(8), Y(8), 4 * escalaX, 4 * escalaY);
                }
                break;
            case DesktopNavigationIcon.Tasks:
                graphics.DrawLines(caneta, [P(2, 10), P(6, 14), P(12, 6)]);
                graphics.DrawLine(caneta, P(12, 7), P(18, 7));
                graphics.DrawLine(caneta, P(12, 13), P(18, 13));
                break;
            case DesktopNavigationIcon.Activity:
                graphics.DrawLines(caneta, [P(2, 13), P(6, 9), P(10, 12), P(15, 5), P(18, 8)]);
                break;
            case DesktopNavigationIcon.Observability:
                graphics.DrawLines(caneta, [P(3, 6), P(8, 10), P(3, 14)]);
                graphics.DrawLine(caneta, P(11, 14), P(17, 14));
                break;
            case DesktopNavigationIcon.Settings:
                graphics.DrawEllipse(caneta, X(5), Y(5), 10 * escalaX, 10 * escalaY);
                graphics.DrawEllipse(caneta, X(8), Y(8), 4 * escalaX, 4 * escalaY);
                graphics.DrawLine(caneta, P(10, 1), P(10, 5));
                graphics.DrawLine(caneta, P(10, 15), P(10, 19));
                graphics.DrawLine(caneta, P(1, 10), P(5, 10));
                graphics.DrawLine(caneta, P(15, 10), P(19, 10));
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hover.Dispose();
            Region?.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class DesktopActivityButton : Button
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;
    private readonly DesktopInteractionAnimator _hover;
    private readonly Font _fonteTitulo;
    private readonly Font _fonteDetalhe;
    private readonly Font _fonteEstado;

    public DesktopActivityButton(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        DesktopPocEffectsPolicy politica,
        Guid reuniaoId,
        string titulo,
        string detalhe,
        string estado)
    {
        _paleta = paleta;
        _tokens = tokens;
        _hover = new DesktopInteractionAnimator(this, tokens.Motion, politica.AnimacoesAtivas);
        _fonteTitulo = new Font(tokens.Tipografia.Interface, 10.5F, FontStyle.Bold, GraphicsUnit.Point);
        _fonteDetalhe = new Font(tokens.Tipografia.Interface, 8.75F, FontStyle.Regular, GraphicsUnit.Point);
        _fonteEstado = new Font(tokens.Tipografia.Interface, 8.75F, FontStyle.Bold, GraphicsUnit.Point);
        ReuniaoId = reuniaoId;
        Titulo = titulo;
        Detalhe = detalhe;
        Estado = estado;
        Text = titulo;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = $"Abrir reunião: {titulo}";
        AccessibleDescription = $"{estado}. {detalhe}. Pressione Enter ou Espaço para abrir a reunião.";
        TabStop = true;
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = paleta.Superficies.Canvas;
        ForeColor = paleta.Texto;
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint,
            true);
    }

    public Guid ReuniaoId { get; }

    public string Titulo { get; }

    public string Detalhe { get; }

    public string Estado { get; }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover.Definir(ativo: true);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover.Definir(ativo: false);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(_paleta.Superficies.Canvas);
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var escala = Math.Max(1F, DeviceDpi / 96F);
        var area = new Rectangle(1, 1, Math.Max(1, ClientSize.Width - 3), Math.Max(1, ClientSize.Height - 3));
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(area, _tokens.Geometria.RaioMedio);
        var fundo = DesktopPocMotion.Misturar(
            _paleta.Superficies.Painel,
            _paleta.Superficies.PainelHover,
            _hover.Valor);
        using (var pincel = new SolidBrush(fundo))
        {
            pevent.Graphics.FillPath(pincel, caminho);
        }

        var corEstado = CorDoEstado();
        using (var borda = new Pen(Focused ? _paleta.Superficies.BordaForte : _paleta.Borda, Focused ? 2F : 1F))
        {
            pevent.Graphics.DrawPath(borda, caminho);
        }

        var x = (int)Math.Round(22F * escala);
        var larguraDireita = (int)Math.Round(190F * escala);
        var larguraTexto = Math.Max(120, ClientSize.Width - x - larguraDireita - (int)Math.Round(18F * escala));
        var topoTitulo = (int)Math.Round(13F * escala);
        var topoDetalhe = (int)Math.Round(42F * escala);
        var alturaLinha = (int)Math.Round(24F * escala);

        using (var marcador = new SolidBrush(corEstado))
        {
            var tamanho = Math.Max(8, (int)Math.Round(8F * escala));
            pevent.Graphics.FillEllipse(
                marcador,
                (int)Math.Round(8F * escala),
                topoTitulo + Math.Max(2, (int)Math.Round(3F * escala)),
                tamanho,
                tamanho);
        }

        TextRenderer.DrawText(
            pevent.Graphics,
            Titulo,
            _fonteTitulo,
            new Rectangle(x, topoTitulo, larguraTexto, alturaLinha),
            _paleta.Texto,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(
            pevent.Graphics,
            Detalhe,
            _fonteDetalhe,
            new Rectangle(x, topoDetalhe, larguraTexto, alturaLinha),
            _paleta.TextoSecundario,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        var direita = ClientSize.Width - larguraDireita;
        TextRenderer.DrawText(
            pevent.Graphics,
            Estado,
            _fonteEstado,
            new Rectangle(direita, topoTitulo, larguraDireita - (int)Math.Round(18F * escala), alturaLinha),
            corEstado,
            TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(
            pevent.Graphics,
            "Abrir reunião  ›",
            _fonteDetalhe,
            new Rectangle(direita, topoDetalhe, larguraDireita - (int)Math.Round(18F * escala), alturaLinha),
            _paleta.TextoSecundario,
            TextFormatFlags.Right | TextFormatFlags.NoPrefix);
    }

    private Color CorDoEstado()
    {
        if (Estado.Contains("Falha", StringComparison.OrdinalIgnoreCase))
        {
            return _paleta.Perigo;
        }

        return Estado is "Ata pronta" or "Concluído"
            ? _paleta.Positivo
            : _paleta.Destaque;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hover.Dispose();
            _fonteTitulo.Dispose();
            _fonteDetalhe.Dispose();
            _fonteEstado.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class DesktopOperationalSteps : Control
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;

    public DesktopOperationalSteps(DesktopPocPalette paleta, DesktopPocDesignTokens tokens)
    {
        _paleta = paleta;
        _tokens = tokens;
        Estado = FluxoOperacionalDesktop.Criar(null);
        DoubleBuffered = true;
        BackColor = paleta.Superficies.PainelElevado;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint,
            true);
        AtualizarAcessibilidade();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EstadoFluxoOperacional Estado { get; private set; }

    public void DefinirEstado(EstadoFluxoOperacional estado)
    {
        Estado = estado;
        AtualizarAcessibilidade();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var itens = Estado.Itens;
        if (itens.Count == 0)
        {
            return;
        }

        var larguraCelula = ClientSize.Width / (float)itens.Count;
        var centroY = 13F;
        using var fonte = new Font(
            _tokens.Tipografia.Interface,
            8F,
            FontStyle.Regular,
            GraphicsUnit.Point);
        for (var indice = 0; indice < itens.Count; indice++)
        {
            var item = itens[indice];
            var centroX = larguraCelula * indice + larguraCelula / 2F;
            if (indice < itens.Count - 1)
            {
                var proximoX = larguraCelula * (indice + 1) + larguraCelula / 2F;
                using var ligacao = new Pen(
                    item.Estado == EstadoItemEtapa.Concluida ? _paleta.Positivo : _paleta.Borda,
                    2F);
                e.Graphics.DrawLine(ligacao, centroX + 7F, centroY, proximoX - 7F, centroY);
            }

            var cor = item.Estado switch
            {
                EstadoItemEtapa.Concluida => _paleta.Positivo,
                EstadoItemEtapa.Atual when item.Etapa == EtapaFluxoOperacional.Falha => _paleta.Perigo,
                EstadoItemEtapa.Atual => _paleta.Destaque,
                _ => _paleta.Borda
            };
            using var preenchimento = new SolidBrush(cor);
            e.Graphics.FillEllipse(preenchimento, centroX - 6F, centroY - 6F, 12F, 12F);
            var areaTexto = new RectangleF(
                larguraCelula * indice + 2F,
                25F,
                Math.Max(1F, larguraCelula - 4F),
                Math.Max(1F, ClientSize.Height - 25F));
            using var texto = new SolidBrush(
                item.Estado == EstadoItemEtapa.Pendente ? _paleta.TextoSecundario : _paleta.Texto);
            using var formato = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisWord
            };
            e.Graphics.DrawString(item.Nome, fonte, texto, areaTexto, formato);
        }
    }

    private void AtualizarAcessibilidade()
    {
        AccessibleName = "Etapas da reunião";
        AccessibleDescription = $"Etapa atual: {Estado.Itens.Single(item => item.Etapa == Estado.Atual).Nome}.";
    }
}

internal sealed class DesktopSignalMeter : Control
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;
    private readonly Color _accentColor;
    private readonly bool _animacoesAtivas;
    private System.Windows.Forms.Timer? _timer;
    private double _valorExibido;
    private int _value;
    private readonly List<int> _historico = [];

    public DesktopSignalMeter(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        DesktopPocEffectsPolicy politica,
        Color accentColor)
    {
        _paleta = paleta;
        _tokens = tokens;
        _accentColor = accentColor;
        _animacoesAtivas = politica.AnimacoesAtivas;
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = paleta.Superficies.PainelElevado;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 100);
            Disponivel = true;
            AccessibleDescription = $"Nível atual: {_value} por cento.";
            _historico.Add(_value);
            if (_historico.Count > 48)
            {
                _historico.RemoveAt(0);
            }
            if (!_animacoesAtivas)
            {
                _valorExibido = _value;
                Invalidate();
                return;
            }

            _timer ??= CriarTimer();
            if (!_timer.Enabled)
            {
                _timer.Start();
            }
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Disponivel { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<int> Historico => _historico;

    public void DefinirSemLeitura()
    {
        Disponivel = false;
        _value = 0;
        _valorExibido = 0;
        _historico.Clear();
        AccessibleDescription = "Sem leitura de nível disponível.";
        _timer?.Stop();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(0, 0, Math.Max(1, ClientSize.Width - 1), Math.Max(1, ClientSize.Height - 1));
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(area, _tokens.Geometria.RaioPequeno);
        using var fundo = new SolidBrush(_paleta.Superficies.PainelElevado);
        e.Graphics.FillPath(fundo, caminho);

        if (!Disponivel)
        {
            using var indisponivel = new Pen(_paleta.TextoSecundario, 1.5F)
            {
                DashStyle = DashStyle.Dash
            };
            e.Graphics.DrawLine(indisponivel, 8, area.Height / 2F, area.Width - 8, area.Height / 2F);
            using var bordaIndisponivel = new Pen(_paleta.Borda, 1F);
            e.Graphics.DrawPath(bordaIndisponivel, caminho);
            return;
        }

        var larguraAtiva = Math.Max(0, (int)Math.Round(area.Width * (_valorExibido / 100D)));
        if (larguraAtiva > 0)
        {
            var preenchimento = new Rectangle(area.X, area.Y, larguraAtiva, area.Height);
            using var energia = new SolidBrush(_accentColor);
            var estado = e.Graphics.Save();
            e.Graphics.SetClip(caminho);
            e.Graphics.FillRectangle(energia, preenchimento);
            e.Graphics.Restore(estado);
        }

        using var grade = new Pen(
            DesktopPocMotion.Misturar(_paleta.Superficies.PainelElevado, _paleta.Fundo, 0.72D),
            1F);
        for (var x = 42; x < area.Width; x += 48)
        {
            e.Graphics.DrawLine(grade, x, 2, x, area.Height - 2);
        }

        using var borda = new Pen(DesktopPocMotion.Misturar(_paleta.Borda, _accentColor, 0.45D), 1F);
        e.Graphics.DrawPath(borda, caminho);

        if (_historico.Count > 1)
        {
            var pontos = _historico.Select((valor, indice) => new PointF(
                indice * area.Width / Math.Max(1F, _historico.Count - 1F),
                area.Height - 2F - ((area.Height - 4F) * valor / 100F)))
                .ToArray();
            using var linha = new Pen(_paleta.Texto, 1.5F);
            e.Graphics.DrawLines(linha, pontos);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Stop();
            _timer?.Dispose();
        }

        base.Dispose(disposing);
    }

    private System.Windows.Forms.Timer CriarTimer()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            var diferenca = _value - _valorExibido;
            if (Math.Abs(diferenca) < 0.75D)
            {
                _valorExibido = _value;
                timer.Stop();
            }
            else
            {
                _valorExibido += diferenca * 0.22D;
            }

            Invalidate();
        };
        return timer;
    }
}

internal sealed class DesktopTextField : UserControl
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;
    private readonly TextBox _editor;
    private readonly string _placeholderText;

    public DesktopTextField(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        string placeholder)
    {
        _paleta = paleta;
        _tokens = tokens;
        _placeholderText = placeholder;
        DoubleBuffered = true;
        Height = 38;
        MinimumSize = new Size(120, 38);
        Padding = new Padding(12, 9, 12, 7);
        BackColor = paleta.Superficies.PainelElevado;
        ForeColor = paleta.Texto;
        Cursor = Cursors.IBeam;
        TabStop = true;

        _editor = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            BackColor = paleta.Superficies.PainelElevado,
            ForeColor = paleta.Texto,
            Font = new Font(tokens.Tipografia.Interface, 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            PlaceholderText = placeholder,
            TabStop = false
        };
        _editor.TextChanged += (_, _) =>
        {
            base.Text = _editor.Text;
            OnTextChanged(EventArgs.Empty);
            AtualizarPlaceholder();
        };
        _editor.Enter += (_, _) =>
        {
            AtualizarPlaceholder();
            Invalidate();
        };
        _editor.Leave += (_, _) =>
        {
            AtualizarPlaceholder();
            Invalidate();
        };
        Controls.Add(_editor);
        Click += (_, _) => FocarEditor();
        AtualizarPlaceholder();
    }

    [AllowNull]
    public override string Text
    {
        get => _editor?.Text ?? base.Text;
        set
        {
            base.Text = value;
            if (_editor is not null && !string.Equals(_editor.Text, value, StringComparison.Ordinal))
            {
                _editor.Text = value;
            }

            AtualizarPlaceholder();
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        FocarEditor();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Region?.Dispose();
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(ClientRectangle, _tokens.Geometria.RaioPequeno);
        Region = new Region(caminho);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(area, _tokens.Geometria.RaioPequeno);
        using var borda = new Pen(
            _editor.Focused ? _paleta.Destaque : _paleta.Borda,
            _editor.Focused ? _tokens.Geometria.BordaFoco : _tokens.Geometria.Borda);
        e.Graphics.DrawPath(borda, caminho);
        if (!_editor.Visible)
        {
            TextRenderer.DrawText(
                e.Graphics,
                _placeholderText,
                _editor.Font,
                new Rectangle(12, 0, Math.Max(1, Width - 24), Height),
                _paleta.TextoSecundario,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    private void AtualizarPlaceholder()
    {
        _editor.Visible = _editor.Focused || !string.IsNullOrEmpty(_editor.Text);
        Invalidate();
    }

    private void FocarEditor()
    {
        _editor.Visible = true;
        _editor.Focus();
    }
}

internal sealed class DesktopSelectField : Control
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;
    private readonly List<string> _opcoes = [];
    private int _selectedIndex = -1;

    public DesktopSelectField(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        IEnumerable<string> opcoes)
    {
        _paleta = paleta;
        _tokens = tokens;
        _opcoes.AddRange(opcoes);
        _selectedIndex = _opcoes.Count > 0 ? 0 : -1;
        Height = 38;
        MinimumSize = new Size(120, 38);
        BackColor = paleta.Superficies.PainelElevado;
        ForeColor = paleta.Texto;
        Font = new Font(tokens.Tipografia.Interface, 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);
    }

    public event EventHandler? SelectedIndexChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var novo = _opcoes.Count == 0 ? -1 : Math.Clamp(value, 0, _opcoes.Count - 1);
            if (_selectedIndex == novo)
            {
                return;
            }

            _selectedIndex = novo;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public override string Text =>
        SelectedIndex >= 0 && SelectedIndex < _opcoes.Count ? _opcoes[SelectedIndex] : string.Empty;

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        MostrarOpcoes();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space || (e.Alt && e.KeyCode == Keys.Down))
        {
            MostrarOpcoes();
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Down && SelectedIndex < _opcoes.Count - 1)
        {
            SelectedIndex++;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up && SelectedIndex > 0)
        {
            SelectedIndex--;
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Region?.Dispose();
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(ClientRectangle, _tokens.Geometria.RaioPequeno);
        Region = new Region(caminho);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(area, _tokens.Geometria.RaioPequeno);
        using var fundo = new SolidBrush(_paleta.Superficies.PainelElevado);
        e.Graphics.FillPath(fundo, caminho);
        using var borda = new Pen(Focused ? _paleta.Destaque : _paleta.Borda, Focused ? 2F : 1F);
        e.Graphics.DrawPath(borda, caminho);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(12, 0, Math.Max(1, Width - 52), Height),
            _paleta.Texto,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        using var separador = new Pen(_paleta.Borda, 1F);
        e.Graphics.DrawLine(separador, Width - 36, 8, Width - 36, Height - 8);
        using var chevron = new Pen(_paleta.TextoSecundario, 1.6F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var centroX = Width - 18;
        var centroY = Height / 2;
        e.Graphics.DrawLines(
            chevron,
            [new Point(centroX - 4, centroY - 2), new Point(centroX, centroY + 2), new Point(centroX + 4, centroY - 2)]);
    }

    private void MostrarOpcoes()
    {
        if (_opcoes.Count == 0)
        {
            return;
        }

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = _paleta.Superficies.PainelElevado,
            ForeColor = _paleta.Texto,
            Font = Font,
            Padding = new Padding(4),
            AutoSize = false,
            Width = Width,
            Height = (_opcoes.Count * 32) + 8
        };
        for (var indice = 0; indice < _opcoes.Count; indice++)
        {
            var indiceCapturado = indice;
            var item = new ToolStripMenuItem(_opcoes[indice])
            {
                Checked = indice == SelectedIndex,
                BackColor = _paleta.Superficies.PainelElevado,
                ForeColor = _paleta.Texto,
                AutoSize = false,
                Size = new Size(Math.Max(1, Width - 8), 32)
            };
            item.Click += (_, _) => SelectedIndex = indiceCapturado;
            menu.Items.Add(item);
        }

        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(this, new Point(0, Height + 2));
    }
}

internal sealed class DesktopToggle : CheckBox
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;

    public DesktopToggle(DesktopPocPalette paleta, DesktopPocDesignTokens tokens)
    {
        _paleta = paleta;
        _tokens = tokens;
        AutoSize = false;
        Size = new Size(112, 34);
        BackColor = paleta.Superficies.Painel;
        ForeColor = paleta.Texto;
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint,
            true);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Text = Checked ? "Ativado" : "Desativado";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(BackColor);
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var trilho = new Rectangle(1, 7, 40, 20);
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(trilho, 10);
        using var fundo = new SolidBrush(Checked ? _paleta.Positivo : _paleta.Superficies.PainelHover);
        pevent.Graphics.FillPath(fundo, caminho);
        using var borda = new Pen(Focused ? _paleta.Destaque : _paleta.Borda, Focused ? 2F : 1F);
        pevent.Graphics.DrawPath(borda, caminho);
        var x = Checked ? trilho.Right - 17 : trilho.Left + 3;
        using var indicador = new SolidBrush(Checked ? Color.White : _paleta.TextoSecundario);
        pevent.Graphics.FillEllipse(indicador, x, trilho.Top + 3, 14, 14);
        using var fonte = new Font(_tokens.Tipografia.Interface, 9F, FontStyle.Regular, GraphicsUnit.Point);
        TextRenderer.DrawText(
            pevent.Graphics,
            Checked ? "Ativado" : "Desativado",
            fonte,
            new Rectangle(50, 0, Width - 50, Height),
            _paleta.Texto,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

internal sealed class DesktopBrandMark : Control
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;

    public DesktopBrandMark(DesktopPocPalette paleta, DesktopPocDesignTokens tokens)
    {
        _paleta = paleta;
        _tokens = tokens;
        DoubleBuffered = true;
        BackColor = paleta.Superficies.PainelElevado;
        Size = new Size(36, 36);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Region?.Dispose();
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(ClientRectangle, _tokens.Geometria.RaioMedio);
        Region = new Region(caminho);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(_paleta.Superficies.PainelElevado);
        var area = new Rectangle(1, 1, Width - 3, Height - 3);
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(area, _tokens.Geometria.RaioMedio);
        using var borda = new Pen(_paleta.Borda, 1F);
        e.Graphics.DrawPath(borda, caminho);
        var centro = new Point(Width / 2, Height / 2);
        var losango = new[]
        {
            new Point(centro.X, centro.Y - 8),
            new Point(centro.X + 5, centro.Y),
            new Point(centro.X, centro.Y + 8),
            new Point(centro.X - 5, centro.Y)
        };
        using var marca = new SolidBrush(_paleta.Destaque);
        e.Graphics.FillPolygon(marca, losango);
    }
}

internal sealed class DesktopInteractionAnimator : IDisposable
{
    private readonly Control _controle;
    private readonly DesktopPocMotionTokens _tokens;
    private readonly bool _animacoesAtivas;
    private System.Windows.Forms.Timer? _timer;
    private double _alvo;

    public DesktopInteractionAnimator(
        Control controle,
        DesktopPocMotionTokens tokens,
        bool animacoesAtivas)
    {
        _controle = controle;
        _tokens = tokens;
        _animacoesAtivas = animacoesAtivas;
    }

    public double Valor { get; private set; }

    public void Definir(bool ativo)
    {
        _alvo = ativo ? 1D : 0D;
        if (!_animacoesAtivas)
        {
            Valor = _alvo;
            _controle.Invalidate();
            return;
        }

        _timer ??= CriarTimer();
        if (!_timer.Enabled)
        {
            _timer.Start();
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private System.Windows.Forms.Timer CriarTimer()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            var passo = timer.Interval / (double)_tokens.FastMs;
            if (Math.Abs(_alvo - Valor) <= passo)
            {
                Valor = _alvo;
                timer.Stop();
            }
            else
            {
                Valor += _alvo > Valor ? passo : -passo;
            }

            _controle.Invalidate();
        };
        return timer;
    }
}

internal static class DesktopPocDrawing
{
    public static GraphicsPath CriarCaminhoArredondado(Rectangle area, int raio)
    {
        var caminho = new GraphicsPath();
        if (area.Width <= 1 || area.Height <= 1 || raio <= 1)
        {
            caminho.AddRectangle(area);
            return caminho;
        }

        var diametro = Math.Min(raio * 2, Math.Min(area.Width, area.Height));
        var arco = new Rectangle(area.X, area.Y, diametro, diametro);
        caminho.AddArc(arco, 180F, 90F);
        arco.X = area.Right - diametro;
        caminho.AddArc(arco, 270F, 90F);
        arco.Y = area.Bottom - diametro;
        caminho.AddArc(arco, 0F, 90F);
        arco.X = area.Left;
        caminho.AddArc(arco, 90F, 90F);
        caminho.CloseFigure();
        return caminho;
    }
}

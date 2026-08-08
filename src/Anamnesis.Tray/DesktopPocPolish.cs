using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Anamnesis.Tray;

/// <summary>
/// Progress bar custom elegante com gradiente, bordas arredondadas e animação suave.
/// Substitui o ProgressBar nativo do Windows que fica horroroso em tema escuro.
/// </summary>
internal sealed class DesktopProgressBar : Control
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;
    private readonly DesktopPocEffectsPolicy _politica;
    private System.Windows.Forms.Timer? _marqueeTimer;
    private double _marqueeOffset;
    private bool _marqueeAtivo;
    private int _valor;
    private int _maximo = 100;

    public DesktopProgressBar(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        DesktopPocEffectsPolicy politica)
    {
        _paleta = paleta;
        _tokens = tokens;
        _politica = politica;
        DoubleBuffered = true;
        Height = 10;
        MinimumSize = new Size(40, 6);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint,
            true);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Valor
    {
        get => _valor;
        set
        {
            _valor = Math.Clamp(value, 0, _maximo);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximo
    {
        get => _maximo;
        set
        {
            _maximo = Math.Max(1, value);
            _valor = Math.Clamp(_valor, 0, _maximo);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MarqueeAtivo
    {
        get => _marqueeAtivo;
        set
        {
            if (_marqueeAtivo == value)
                return;
            _marqueeAtivo = value;
            if (value)
            {
                _marqueeTimer ??= CriarMarqueeTimer();
                _marqueeTimer.Start();
            }
            else
            {
                _marqueeTimer?.Stop();
            }
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        using var caminhoFundo = DesktopPocDrawing.CriarCaminhoArredondado(area, area.Height / 2);

        // Fundo sutil
        using var fundo = new SolidBrush(_paleta.Superficies.PainelHover);
        e.Graphics.FillPath(fundo, caminhoFundo);

        if (_marqueeAtivo)
        {
            DesenharMarquee(e.Graphics, area);
        }
        else
        {
            DesencherPreenchimento(e.Graphics, area);
        }

        // Borda sutil
        using var borda = new Pen(Color.FromArgb(40, _paleta.Borda), 1F);
        e.Graphics.DrawPath(borda, caminhoFundo);
    }

    private void DesenharMarquee(Graphics g, Rectangle area)
    {
        var larguraBloco = Math.Max(40, area.Width / 4);
        var x = (int)((_marqueeOffset * (area.Width + larguraBloco)) - larguraBloco);

        var bloco = new Rectangle(x, 1, larguraBloco, area.Height - 2);
        using var caminhoBloco = DesktopPocDrawing.CriarCaminhoArredondado(bloco, bloco.Height / 2);
        using var gradiente = new LinearGradientBrush(
            bloco,
            Color.FromArgb(0, _paleta.Destaque),
            _paleta.Destaque,
            LinearGradientMode.Horizontal);
        g.FillPath(gradiente, caminhoBloco);
    }

    private void DesencherPreenchimento(Graphics g, Rectangle area)
    {
        if (_valor <= 0) return;
        var largura = (int)Math.Round(area.Width * (_valor / (double)_maximo));
        if (largura < 2) return;

        var preenchimento = new Rectangle(1, 1, largura - 2, area.Height - 3);
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(preenchimento, preenchimento.Height / 2);
        using var gradiente = new LinearGradientBrush(
            preenchimento,
            DesktopPocMotion.Misturar(_paleta.Destaque, Color.White, 0.15),
            _paleta.Destaque,
            LinearGradientMode.Horizontal);
        g.FillPath(gradiente, caminho);
    }

    private System.Windows.Forms.Timer CriarMarqueeTimer()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 30 };
        timer.Tick += (_, _) =>
        {
            _marqueeOffset += 0.012;
            if (_marqueeOffset > 1.0)
                _marqueeOffset = 0;
            Invalidate();
        };
        return timer;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _marqueeTimer?.Stop();
            _marqueeTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Badge/Pill de status colorida com bordas arredondadas e cor automática por estado.
/// Substitui o Label simples usado para mostrar status de reuniões.
/// </summary>
internal sealed class DesktopStatusBadge : Control
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;
    private Color _corFundo;
    private Color _corTexto;

    public DesktopStatusBadge(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        string texto)
    {
        _paleta = paleta;
        _tokens = tokens;
        Text = texto;
        DoubleBuffered = true;
        AutoSize = true;
        Height = 24;
        Cursor = Cursors.Hand;
        DefinirCoresPorStatus(texto);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint,
            true);
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text { get; set; } = string.Empty;

    public void AtualizarStatus(string status)
    {
        Text = status;
        DefinirCoresPorStatus(status);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(area, area.Height / 2);
        using var fundo = new SolidBrush(_corFundo);
        e.Graphics.FillPath(fundo, caminho);

        // Bolinha indicadora
        var raio = 4;
        using var indicador = new SolidBrush(_corTexto);
        e.Graphics.FillEllipse(indicador, 8, (area.Height - raio * 2) / 2, raio * 2, raio * 2);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            new Font(_tokens.Tipografia.Interface, 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            new Rectangle(18, 0, Math.Max(1, area.Width - 22), area.Height),
            _corTexto,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Region?.Dispose();
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(ClientRectangle, ClientSize.Height / 2);
        Region = new Region(caminho);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var texto = TextRenderer.MeasureText(
            Text,
            new Font(_tokens.Tipografia.Interface, 8.5F, FontStyle.Bold, GraphicsUnit.Point));
        return new Size(Math.Max(72, texto.Width + 28), 24);
    }

    private void DefinirCoresPorStatus(string status)
    {
        var s = status.ToLowerInvariant();
        if (s.Contains("concluído") || s.Contains("pronto") || s.Contains("ata pronta") || s.Contains("saudável"))
        {
            _corFundo = _paleta.FundoPositivo;
            _corTexto = _paleta.Positivo;
        }
        else if (s.Contains("gravando") || s.Contains("ao vivo") || s.Contains("perigo"))
        {
            _corFundo = Color.FromArgb(45, 25, 28);
            _corTexto = _paleta.Perigo;
        }
        else if (s.Contains("processando") || s.Contains("transcrevendo") || s.Contains("gerando"))
        {
            _corFundo = _paleta.FundoDestaque;
            _corTexto = _paleta.Destaque;
        }
        else if (s.Contains("erro") || s.Contains("falha"))
        {
            _corFundo = Color.FromArgb(50, 22, 26);
            _corTexto = _paleta.Perigo;
        }
        else if (s.Contains("aviso") || s.Contains("pendente"))
        {
            _corFundo = Color.FromArgb(45, 38, 22);
            _corTexto = _paleta.Destaque;
        }
        else
        {
            _corFundo = _paleta.Superficies.PainelHover;
            _corTexto = _paleta.TextoSecundario;
        }
    }
}

/// <summary>
/// Painel com sombra suave ao redor e comportamento similar ao DesktopSurfacePanel.
/// Não deriva de DesktopSurfacePanel pois ele é sealed.
/// </summary>
internal sealed class DesktopShadowPanel : Panel
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;
    private readonly DesktopPocEffectsPolicy _politica;
    private readonly DesktopInteractionAnimator _hover;
    private int _cornerRadius;
    private bool _interactive;

    public DesktopShadowPanel(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        DesktopPocEffectsPolicy politica,
        DesktopSurfaceVariant variant = DesktopSurfaceVariant.Base)
    {
        _paleta = paleta;
        _tokens = tokens;
        _politica = politica;
        _cornerRadius = tokens.Geometria.RaioGrande;
        _hover = new DesktopInteractionAnimator(this, tokens.Motion, politica.AnimacoesAtivas);
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = CorDaSuperficie(variant);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
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
    public bool Interactive
    {
        get => _interactive;
        set
        {
            _interactive = value;
            Invalidate();
        }
    }

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

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        AtualizarRegiao();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(area, CornerRadius);

        // Camadas de sombra
        for (var i = 3; i >= 1; i--)
        {
            var alpha = (int)(8 * (4 - i));
            if (alpha <= 0) continue;
            var offset = i;
            var areaSombra = new Rectangle(
                offset,
                offset + 1,
                ClientSize.Width - offset * 2 - 1,
                ClientSize.Height - offset * 2 - 1);
            using var caminhoSombra = DesktopPocDrawing.CriarCaminhoArredondado(areaSombra, CornerRadius);
            using var sombra = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
            e.Graphics.FillPath(sombra, caminhoSombra);
        }

        var fundo = BackColor;
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
            return;

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

    private static Color CorDaSuperficie(DesktopSurfaceVariant variant) => variant switch
    {
        DesktopSurfaceVariant.Chrome => Color.FromArgb(24, 33, 44),
        DesktopSurfaceVariant.Navigation => Color.FromArgb(14, 20, 28),
        DesktopSurfaceVariant.Elevated => Color.FromArgb(24, 33, 44),
        DesktopSurfaceVariant.Console => Color.FromArgb(9, 13, 18),
        _ => Color.FromArgb(20, 27, 36)
    };
}

/// <summary>
/// Linha contínua e acionável do histórico de reuniões.
/// </summary>
internal sealed class DesktopReuniaoListItem : Panel
{
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens;
    private readonly DesktopInteractionAnimator _hover;

    public DesktopReuniaoListItem(
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        DesktopPocEffectsPolicy politica,
        ReuniaoDesktopPoc reuniao)
    {
        _paleta = paleta;
        _tokens = tokens;
        _hover = new DesktopInteractionAnimator(this, tokens.Motion, politica.AnimacoesAtivas);
        Height = string.IsNullOrWhiteSpace(reuniao.TrechoCorrespondente) ? 76 : 102;
        Margin = Padding.Empty;
        Padding = new Padding(18, 12, 18, 10);
        BackColor = paleta.Superficies.Painel;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = true;
        AccessibleRole = AccessibleRole.ListItem;
        AccessibleName = $"Abrir reunião {reuniao.Titulo}";
        AccessibleDescription = $"{reuniao.Plataforma}, {reuniao.Data}, {reuniao.Duracao}, estado {reuniao.Status}";
        AccessibleDefaultActionDescription = "Abrir reunião";
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        ConstruirLayout(reuniao);
    }

    private void ConstruirLayout(ReuniaoDesktopPoc reuniao)
    {
        var titulo = new Label
        {
            Text = reuniao.Titulo,
            AutoSize = true,
            ForeColor = _paleta.Texto,
            Font = new Font(_tokens.Tipografia.Interface, 11F, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(18, 13)
        };

        var detalhe = new Label
        {
            Text = $"{reuniao.Plataforma}  •  {reuniao.Data}",
            AutoSize = true,
            ForeColor = _paleta.TextoSecundario,
            Font = new Font(_tokens.Tipografia.Interface, 9F, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(18, 39)
        };

        Label? trecho = null;
        if (!string.IsNullOrWhiteSpace(reuniao.TrechoCorrespondente))
        {
            trecho = new Label
            {
                Text = $"{reuniao.SecaoCorrespondente}: {reuniao.TrechoCorrespondente}",
                AutoEllipsis = true,
                ForeColor = _paleta.Destaque,
                Font = new Font(_tokens.Tipografia.Interface, 8.5F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(18, 65),
                Size = new Size(650, 21),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
        }

        var badge = new DesktopStatusBadge(_paleta, _tokens, reuniao.Status)
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        badge.Location = new Point(Width - badge.Width - 18, 12);
        Resize += (_, _) => badge.Left = ClientSize.Width - badge.Width - 18;

        var duracao = new Label
        {
            Text = reuniao.Duracao,
            AutoSize = true,
            ForeColor = _paleta.TextoSecundario,
            Font = new Font(_tokens.Tipografia.Mono, 9F, FontStyle.Regular, GraphicsUnit.Point),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        duracao.Location = new Point(Width - duracao.Width - 18, 43);
        Resize += (_, _) => duracao.Left = ClientSize.Width - duracao.Width - 18;

        Controls.Add(duracao);
        Controls.Add(badge);
        if (trecho is not null)
        {
            Controls.Add(trecho);
        }
        Controls.Add(detalhe);
        Controls.Add(titulo);

        foreach (Control filho in Controls)
        {
            EncaminharInteracao(filho);
        }
    }

    private void EncaminharInteracao(Control controle)
    {
        controle.Cursor = Cursors.Hand;
        controle.Click += (_, evento) => OnClick(evento);
        controle.MouseEnter += (_, _) => _hover.Definir(ativo: true);
        controle.MouseLeave += (_, _) => AtualizarHoverPelaPosicaoDoCursor();
        foreach (Control filho in controle.Controls)
        {
            EncaminharInteracao(filho);
        }
    }

    private void AtualizarHoverPelaPosicaoDoCursor() =>
        _hover.Definir(ClientRectangle.Contains(PointToClient(Cursor.Position)));

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover.Definir(ativo: true);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        AtualizarHoverPelaPosicaoDoCursor();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        Invalidate();
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
            return;

        var fundo = _paleta.Superficies.Painel;
        if (_hover.Valor > 0D)
        {
            fundo = DesktopPocMotion.Misturar(fundo, _paleta.Superficies.PainelHover, _hover.Valor);
        }

        e.Graphics.Clear(fundo);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
            return;

        using var separador = new Pen(_paleta.Borda, 1F);
        e.Graphics.DrawLine(separador, 18, ClientSize.Height - 1, ClientSize.Width - 18, ClientSize.Height - 1);

        if (_hover.Valor > 0.05D || Focused)
        {
            using var marcador = new Pen(_paleta.Destaque, 3F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            e.Graphics.DrawLine(marcador, 2F, 14F, 2F, ClientSize.Height - 14F);
        }

        if (Focused)
        {
            var foco = Rectangle.Inflate(ClientRectangle, -6, -5);
            ControlPaint.DrawFocusRectangle(e.Graphics, foco, _paleta.Texto, BackColor);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hover.Dispose();
        }
        base.Dispose(disposing);
    }
}

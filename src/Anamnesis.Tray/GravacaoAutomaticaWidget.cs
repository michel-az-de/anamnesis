using System.Drawing.Drawing2D;

namespace Anamnesis.Tray;

internal sealed class GravacaoAutomaticaWidget : Form
{
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private readonly Func<TimeSpan> _obterDuracao;
    private readonly Func<Task> _encerrar;
    private readonly Label _estado;
    private readonly Label _duracao;
    private readonly DesktopActionButton _botaoEncerrar;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private bool _encerrando;

    public GravacaoAutomaticaWidget(
        TemaDesktopPoc tema,
        DesktopPocEffectsPolicy politicaVisual,
        GravacaoAutomaticaInfo info,
        Func<TimeSpan> obterDuracao,
        Action abrir,
        Func<Task> encerrar)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(obterDuracao);
        ArgumentNullException.ThrowIfNull(abrir);
        ArgumentNullException.ThrowIfNull(encerrar);

        _obterDuracao = obterDuracao;
        _encerrar = encerrar;
        var paleta = DesktopPocPalette.Criar(tema);
        var tokens = DesktopPocDesignTokens.Padrao;

        Text = "Gravação automática em andamento";
        AccessibleName = "Indicador de gravação automática";
        AccessibleDescription = $"Gravando automaticamente {info.Titulo} em {info.Plataforma}.";
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = paleta.Superficies.BordaForte;
        ClientSize = new Size(408, 148);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Padding = new Padding(1);

        var superficie = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = paleta.Superficies.PainelElevado
        };
        Controls.Add(superficie);

        var ponto = new Panel
        {
            AccessibleName = "Gravação ativa",
            BackColor = paleta.Perigo,
            Location = new Point(20, 19),
            Size = new Size(10, 10),
            TabStop = false
        };
        ponto.Paint += (_, evento) =>
        {
            evento.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pincel = new SolidBrush(paleta.Perigo);
            evento.Graphics.FillEllipse(pincel, ponto.ClientRectangle);
        };
        ponto.BackColor = paleta.Superficies.PainelElevado;

        _estado = new Label
        {
            Text = "GRAVANDO AUTOMATICAMENTE",
            AutoSize = true,
            ForeColor = paleta.Perigo,
            Font = new Font(tokens.Tipografia.Interface, 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(38, 15)
        };
        var titulo = new Label
        {
            Text = info.Titulo,
            AutoEllipsis = true,
            ForeColor = paleta.Texto,
            Font = new Font(tokens.Tipografia.Interface, 11F, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(20, 40),
            Size = new Size(278, 24)
        };
        var plataforma = new Label
        {
            Text = $"{info.Plataforma}  •  detecção local",
            AutoEllipsis = true,
            ForeColor = paleta.TextoSecundario,
            Font = new Font(tokens.Tipografia.Interface, 8.5F, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(20, 65),
            Size = new Size(278, 20)
        };
        _duracao = new Label
        {
            Text = "00:00",
            AccessibleName = "Tempo de gravação",
            ForeColor = paleta.Texto,
            Font = new Font(tokens.Tipografia.Mono, 18F, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(305, 34),
            Size = new Size(82, 34),
            TextAlign = ContentAlignment.MiddleRight
        };

        var botaoAbrir = new DesktopActionButton(
            paleta,
            tokens,
            politicaVisual,
            DesktopActionVariant.Secondary)
        {
            Text = "Abrir",
            Location = new Point(20, 96),
            Size = new Size(92, 36),
            MinimumSize = new Size(92, 36)
        };
        botaoAbrir.Click += (_, _) => abrir();
        _botaoEncerrar = new DesktopActionButton(
            paleta,
            tokens,
            politicaVisual,
            DesktopActionVariant.Danger)
        {
            Text = "Encerrar",
            Location = new Point(122, 96),
            Size = new Size(108, 36),
            MinimumSize = new Size(108, 36)
        };
        _botaoEncerrar.Click += async (_, _) => await EncerrarAsync();
        var privacidade = new Label
        {
            Text = "Áudio local",
            ForeColor = paleta.TextoSecundario,
            Font = new Font(tokens.Tipografia.Interface, 8F, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(286, 103),
            Size = new Size(101, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        superficie.Controls.Add(privacidade);
        superficie.Controls.Add(_botaoEncerrar);
        superficie.Controls.Add(botaoAbrir);
        superficie.Controls.Add(_duracao);
        superficie.Controls.Add(plataforma);
        superficie.Controls.Add(titulo);
        superficie.Controls.Add(_estado);
        superficie.Controls.Add(ponto);

        _timer.Tick += (_, _) => AtualizarAgora();
        Shown += (_, _) =>
        {
            Posicionar();
            AtualizarAgora();
            _timer.Start();
        };
    }

    internal bool ExibeSemAtivar => ShowWithoutActivation;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parametros = base.CreateParams;
            parametros.ExStyle |= WsExToolWindow | WsExNoActivate;
            return parametros;
        }
    }

    internal void AtualizarAgora()
    {
        var duracao = _obterDuracao();
        if (duracao < TimeSpan.Zero)
        {
            duracao = TimeSpan.Zero;
        }

        _duracao.Text = duracao.TotalHours >= 1
            ? $"{(int)duracao.TotalHours:00}:{duracao.Minutes:00}:{duracao.Seconds:00}"
            : $"{(int)duracao.TotalMinutes:00}:{duracao.Seconds:00}";
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Region?.Dispose();
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
        {
            return;
        }

        using var caminho = DesktopPocDrawing.CriarCaminhoArredondado(
            ClientRectangle,
            DesktopPocDesignTokens.Padrao.Geometria.RaioMedio);
        Region = new Region(caminho);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            Region?.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task EncerrarAsync()
    {
        if (_encerrando)
        {
            return;
        }

        _encerrando = true;
        _botaoEncerrar.Enabled = false;
        _estado.Text = "ENCERRANDO COM SEGURANÇA";
        try
        {
            await _encerrar();
            Hide();
        }
        catch (Exception)
        {
            _estado.Text = "NÃO FOI POSSÍVEL ENCERRAR";
            _botaoEncerrar.Enabled = true;
            _encerrando = false;
        }
    }

    private void Posicionar()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(
            Math.Max(area.Left, area.Right - Width - 16),
            Math.Max(area.Top, area.Bottom - Height - 16));
    }
}

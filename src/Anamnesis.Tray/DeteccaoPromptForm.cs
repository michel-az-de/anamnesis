using Anamnesis.Application.Modelos;

namespace Anamnesis.Tray;

internal sealed class DeteccaoPromptForm : Form, IDeteccaoPrompt
{
    private readonly NotifyIcon? _icone;
    private readonly TemaDesktopPoc _tema;
    private readonly Label _categoria;
    private readonly Label _titulo;
    private readonly Label _descricao;
    private readonly Label _contador;
    private readonly DesktopActionButton _iniciar;
    private readonly DesktopActionButton _ignorar;
    private readonly DesktopActionButton _silenciar;
    private readonly DesktopActionButton _cancelar;
    private TaskCompletionSource<AcaoSugestaoDeteccao>? _respostaSugestao;
    private CancellationTokenRegistration _registroCancelamento;
    private ModoPrompt _modo;
    private int _cancelamentoInicio;
    private int _cancelamentoEncerramento;
    private bool _descartando;

    public DeteccaoPromptForm(
        NotifyIcon? icone,
        TemaDesktopPoc tema,
        DesktopPocEffectsPolicy efeitos)
    {
        _icone = icone;
        _tema = tema;
        var paleta = DesktopPocPalette.Criar(tema);
        var tokens = DesktopPocDesignTokens.Padrao;

        Text = "Anamnesis • Captura";
        AccessibleName = "Prompt de detecção de reunião";
        AccessibleDescription = "Sugere e confirma ações locais de gravação.";
        ClientSize = new Size(460, 246);
        MinimumSize = Size;
        MaximumSize = Size;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = paleta.Superficies.PainelElevado;
        ForeColor = paleta.Texto;
        Opacity = 1D;
        AllowTransparency = false;

        var conteudo = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = paleta.Superficies.PainelElevado,
            Padding = new Padding(tokens.Espacamento.Xl),
            ColumnCount = 1,
            RowCount = 5
        };
        conteudo.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        conteudo.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        conteudo.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        conteudo.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        conteudo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _categoria = new Label
        {
            AutoSize = true,
            Text = "DETECÇÃO LOCAL",
            Font = new Font(tokens.Tipografia.Interface, 8.5F, FontStyle.Bold),
            ForeColor = paleta.Destaque,
            AccessibleName = "Categoria da detecção"
        };
        _titulo = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(tokens.Tipografia.Display, 17F, FontStyle.Bold),
            ForeColor = paleta.Texto,
            AccessibleName = "Título da detecção"
        };
        _descricao = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font(tokens.Tipografia.Interface, 9.5F),
            ForeColor = paleta.TextoSecundario,
            AccessibleName = "Descrição da detecção"
        };
        _contador = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(tokens.Tipografia.Mono, 12F, FontStyle.Bold),
            ForeColor = paleta.Destaque,
            AccessibleName = "Tempo restante"
        };

        _iniciar = CriarBotao(
            "Iniciar gravação",
            "deteccao_iniciar",
            DesktopActionVariant.Primary,
            paleta,
            tokens,
            efeitos);
        _ignorar = CriarBotao(
            "Ignorar",
            "deteccao_ignorar",
            DesktopActionVariant.Secondary,
            paleta,
            tokens,
            efeitos);
        _silenciar = CriarBotao(
            "Silenciar 1 h",
            "deteccao_silenciar",
            DesktopActionVariant.Ghost,
            paleta,
            tokens,
            efeitos);
        _cancelar = CriarBotao(
            "Cancelar",
            "deteccao_cancelar",
            DesktopActionVariant.Secondary,
            paleta,
            tokens,
            efeitos);

        _iniciar.Click += (_, _) => ConcluirSugestao(AcaoSugestaoDeteccao.Iniciar);
        _ignorar.Click += (_, _) => ConcluirSugestao(AcaoSugestaoDeteccao.Ignorar);
        _silenciar.Click += (_, _) => ConcluirSugestao(AcaoSugestaoDeteccao.Silenciar);
        _cancelar.Click += (_, _) => CancelarModoAtual();

        var acoes = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = paleta.Superficies.PainelElevado,
            Padding = new Padding(0, tokens.Espacamento.Sm, 0, 0)
        };
        acoes.Controls.AddRange([_iniciar, _ignorar, _silenciar, _cancelar]);

        conteudo.Controls.Add(_categoria, 0, 0);
        conteudo.Controls.Add(_titulo, 0, 1);
        conteudo.Controls.Add(_descricao, 0, 2);
        conteudo.Controls.Add(_contador, 0, 3);
        conteudo.Controls.Add(acoes, 0, 4);
        Controls.Add(conteudo);
        Shown += (_, _) => WindowsTitleBarTheme.Aplicar(this, _tema, paleta);
    }

    public Task<AcaoSugestaoDeteccao> SugerirInicioAsync(
        PlataformaLocal plataforma,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plataforma);
        cancellationToken.ThrowIfCancellationRequested();
        EncerrarSugestaoPendente(AcaoSugestaoDeteccao.Fechar);
        _modo = ModoPrompt.Sugestao;
        _categoria.Text = "CHAMADA DETECTADA";
        _titulo.Text = plataforma.Nome;
        _descricao.Text = "O microfone e o áudio local indicam uma reunião em andamento.";
        _contador.Text = "Escolha antes de continuar.";
        DefinirAcoes(sugestao: true, cancelamento: false);
        _respostaSugestao = new TaskCompletionSource<AcaoSugestaoDeteccao>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _registroCancelamento = cancellationToken.Register(() =>
            ExecutarNaInterface(() =>
            {
                _respostaSugestao?.TrySetCanceled(cancellationToken);
                Ocultar();
            }));
        Exibir();
        return _respostaSugestao.Task;
    }

    public bool ConsumirCancelamentoInicio() =>
        Interlocked.Exchange(ref _cancelamentoInicio, 0) != 0;

    public void MostrarContagemInicio(PlataformaLocal plataforma, TimeSpan restante)
    {
        ArgumentNullException.ThrowIfNull(plataforma);
        _modo = ModoPrompt.ContagemInicio;
        _categoria.Text = "INÍCIO AUTOMÁTICO";
        _titulo.Text = plataforma.Nome;
        _descricao.Text = "A gravação começará automaticamente. Você ainda pode cancelar.";
        _contador.Text = $"Iniciando em {Segundos(restante)} s";
        _cancelar.Text = "Cancelar início";
        DefinirAcoes(sugestao: false, cancelamento: true);
        Exibir();
    }

    public void FecharContagemInicio()
    {
        if (_modo == ModoPrompt.ContagemInicio)
        {
            Ocultar();
        }
    }

    public bool ConsumirCancelamentoEncerramento() =>
        Interlocked.Exchange(ref _cancelamentoEncerramento, 0) != 0;

    public void MostrarAvisoEncerramento(TimeSpan restante)
    {
        _modo = ModoPrompt.AvisoEncerramento;
        _categoria.Text = "SINAIS AUSENTES";
        _titulo.Text = "Encerrar a gravação?";
        _descricao.Text = "Nenhum sinal de chamada foi percebido por dois minutos.";
        _contador.Text = $"Encerrando em {Segundos(restante)} s";
        _cancelar.Text = "Manter gravando";
        DefinirAcoes(sugestao: false, cancelamento: true);
        Exibir();
    }

    public void FecharAvisoEncerramento()
    {
        if (_modo == ModoPrompt.AvisoEncerramento)
        {
            Ocultar();
        }
    }

    public void NotificarInicioAutomatico() =>
        _icone?.ShowBalloonTip(
            4000,
            "Anamnesis está gravando",
            "A captura automática foi confirmada. Use o Tray para encerrar a qualquer momento.",
            ToolTipIcon.Info);

    public void MostrarFalhaSegura() =>
        _icone?.ShowBalloonTip(
            5000,
            "A captura não foi iniciada",
            "Confira o console local de observabilidade e tente iniciar manualmente.",
            ToolTipIcon.Warning);

    public void Fechar()
    {
        EncerrarSugestaoPendente(AcaoSugestaoDeteccao.Fechar);
        Ocultar();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_descartando && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            if (_modo == ModoPrompt.Sugestao)
            {
                ConcluirSugestao(AcaoSugestaoDeteccao.Fechar);
            }
            else
            {
                CancelarModoAtual();
            }

            return;
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _descartando = true;
            _registroCancelamento.Dispose();
            _respostaSugestao?.TrySetResult(AcaoSugestaoDeteccao.Fechar);
        }

        base.Dispose(disposing);
    }

    private void CancelarModoAtual()
    {
        if (_modo == ModoPrompt.ContagemInicio)
        {
            Interlocked.Exchange(ref _cancelamentoInicio, 1);
        }
        else if (_modo == ModoPrompt.AvisoEncerramento)
        {
            Interlocked.Exchange(ref _cancelamentoEncerramento, 1);
        }

        Ocultar();
    }

    private void ConcluirSugestao(AcaoSugestaoDeteccao acao)
    {
        if (_modo != ModoPrompt.Sugestao)
        {
            return;
        }

        EncerrarSugestaoPendente(acao);
        Ocultar();
    }

    private void EncerrarSugestaoPendente(AcaoSugestaoDeteccao acao)
    {
        _registroCancelamento.Dispose();
        _respostaSugestao?.TrySetResult(acao);
        _respostaSugestao = null;
    }

    private void DefinirAcoes(bool sugestao, bool cancelamento)
    {
        _iniciar.Visible = sugestao;
        _ignorar.Visible = sugestao;
        _silenciar.Visible = sugestao;
        _cancelar.Visible = cancelamento;
    }

    private void Exibir()
    {
        if (!Visible)
        {
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(
                area.Right - Width - 24,
                area.Bottom - Height - 24);
            Show();
        }

        Activate();
    }

    private void Ocultar()
    {
        _modo = ModoPrompt.Nenhum;
        if (Visible)
        {
            Hide();
        }
    }

    private void ExecutarNaInterface(Action acao)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired && IsHandleCreated)
        {
            BeginInvoke(acao);
            return;
        }

        acao();
    }

    private static int Segundos(TimeSpan restante) =>
        Math.Max(0, (int)Math.Ceiling(restante.TotalSeconds));

    private static DesktopActionButton CriarBotao(
        string texto,
        string nomeAcessivel,
        DesktopActionVariant variante,
        DesktopPocPalette paleta,
        DesktopPocDesignTokens tokens,
        DesktopPocEffectsPolicy efeitos) =>
        new(paleta, tokens, efeitos, variante)
        {
            Text = texto,
            AccessibleName = nomeAcessivel,
            MinimumSize = new Size(88, 40),
            AutoSize = true,
            Margin = new Padding(0, 0, tokens.Espacamento.Sm, 0)
        };

    private enum ModoPrompt
    {
        Nenhum,
        Sugestao,
        ContagemInicio,
        AvisoEncerramento
    }
}

using System.Diagnostics;
using Anamnesis.Infrastructure.Configuracao;

namespace Anamnesis.Tray;

internal static class DesktopStartupExperience
{
    private static readonly string[] ModosSilenciosos =
    [
        "--background",
        "--diagnostico-deteccao",
        "--gravar-teste-segundos",
        "--encerrar-para-atualizacao",
        "--validar-instancia"
    ];

    public static bool DeveExibirSplash(IReadOnlyList<string> argumentos) =>
        !argumentos.Any(argumento =>
            ModosSilenciosos.Contains(argumento, StringComparer.OrdinalIgnoreCase));

    public static bool DeveExibirWizard(
        ConfiguracaoAnamnesis configuracao,
        IReadOnlyList<string> argumentos) =>
        !configuracao.PrimeiroUsoConcluido && DeveExibirSplash(argumentos);
}

internal sealed record DesktopConfigurationDraft(
    string DiretorioArquivo,
    string EnderecoObs,
    string NomeCli,
    string CaminhoExecutavelCli,
    bool IniciarComWindows)
{
    public static DesktopConfigurationDraft From(
        ConfiguracaoAnamnesis configuracao,
        bool iniciarComWindows) =>
        new(
            configuracao.DiretorioArquivo,
            configuracao.EnderecoObs,
            configuracao.NomeCli,
            configuracao.CaminhoExecutavelCli,
            iniciarComWindows);

    public ConfiguracaoAnamnesis Build(ConfiguracaoAnamnesis original)
    {
        if (string.IsNullOrWhiteSpace(DiretorioArquivo))
        {
            throw new InvalidOperationException("Informe uma pasta local para as reuniões.");
        }

        if (!Uri.TryCreate(EnderecoObs, UriKind.Absolute, out var endereco) ||
            endereco.Scheme is not ("ws" or "wss"))
        {
            throw new InvalidOperationException("O endereço do OBS deve começar com ws:// ou wss://.");
        }

        if (string.IsNullOrWhiteSpace(NomeCli))
        {
            throw new InvalidOperationException("Informe o nome da CLI usada para gerar atas.");
        }

        return original with
        {
            PrimeiroUsoConcluido = true,
            DiretorioArquivo = Path.GetFullPath(Environment.ExpandEnvironmentVariables(DiretorioArquivo.Trim())),
            EnderecoObs = EnderecoObs.Trim(),
            NomeCli = NomeCli.Trim(),
            CaminhoExecutavelCli = CaminhoExecutavelCli.Trim()
        };
    }
}

internal sealed class DesktopSplashForm : Form
{
    private readonly DesktopBrandMark _marca;
    private readonly DesktopProgressBar _progresso;
    private readonly bool _animacoesAtivas;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly Stopwatch _relogio = new();

    internal DesktopSplashForm(
        TemaDesktopPoc tema,
        DesktopPocEffectsPolicy politica)
    {
        var paleta = DesktopPocPalette.Criar(tema);
        var tokens = DesktopPocDesignTokens.Padrao;
        _animacoesAtivas = politica.AnimacoesAtivas;
        Text = "Anamnesis";
        AccessibleName = "Anamnesis iniciando";
        AccessibleDescription = "Apresentação da marca durante a abertura do aplicativo.";
        Icon = IconeAnamnesis.Carregar();
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 316);
        BackColor = paleta.Superficies.Canvas;
        ShowInTaskbar = true;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        var superficie = new DesktopSurfacePanel(
            paleta,
            tokens,
            politica,
            DesktopSurfaceVariant.Elevated)
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(44),
            CornerRadius = tokens.Geometria.RaioHeroi
        };
        _marca = new DesktopBrandMark(paleta, tokens)
        {
            Location = new Point(44, 44),
            Size = new Size(92, 92),
            AccessibleName = "Marca Anamnesis"
        };
        _marca.DefinirProgresso(_animacoesAtivas ? 0D : 1D);
        superficie.Controls.Add(_marca);
        superficie.Controls.Add(CriarLabel(
            "ANAMNESIS",
            new Point(164, 54),
            24F,
            FontStyle.Bold,
            paleta.Texto,
            tokens.Tipografia.Display));
        superficie.Controls.Add(CriarLabel(
            "O que foi dito, lembrado com clareza.",
            new Point(166, 100),
            10.5F,
            FontStyle.Regular,
            paleta.TextoSecundario,
            tokens.Tipografia.Interface));
        superficie.Controls.Add(CriarLabel(
            "Preparando seu espaço local",
            new Point(44, 214),
            9.5F,
            FontStyle.Bold,
            paleta.TextoSecundario,
            tokens.Tipografia.Interface));
        _progresso = new DesktopProgressBar(paleta, tokens, politica)
        {
            Location = new Point(44, 250),
            Size = new Size(432, 4),
            Valor = _animacoesAtivas ? 0 : 100,
            AccessibleName = "Progresso da abertura"
        };
        superficie.Controls.Add(_progresso);
        Controls.Add(superficie);

        _timer.Tick += (_, _) => AvancarAnimacao();
        Shown += (_, _) =>
        {
            if (!_animacoesAtivas)
            {
                _timer.Interval = 180;
                _timer.Start();
                return;
            }

            _relogio.Restart();
            _timer.Start();
        };
    }

    public static void Exibir(
        TemaDesktopPoc tema,
        DesktopPocEffectsPolicy politica)
    {
        using var splash = new DesktopSplashForm(tema, politica);
        splash.ShowDialog();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AvancarAnimacao()
    {
        if (!_animacoesAtivas)
        {
            _timer.Stop();
            Close();
            return;
        }

        var linear = Math.Clamp(_relogio.Elapsed.TotalMilliseconds / 900D, 0D, 1D);
        var suave = DesktopPocMotion.SuavizarSaida(linear);
        _marca.DefinirProgresso(suave);
        _progresso.Valor = (int)Math.Round(suave * 100D);
        if (_relogio.ElapsedMilliseconds < 1050)
        {
            return;
        }

        _timer.Stop();
        Close();
    }

    private static Label CriarLabel(
        string texto,
        Point local,
        float tamanho,
        FontStyle estilo,
        Color cor,
        string familia) =>
        new()
        {
            Text = texto,
            AutoSize = true,
            Location = local,
            ForeColor = cor,
            BackColor = Color.Transparent,
            Font = new Font(familia, tamanho, estilo, GraphicsUnit.Point)
        };
}

internal sealed class PrimeiroAcessoForm : Form
{
    private readonly ConfiguracaoAnamnesis _original;
    private readonly DesktopPocPalette _paleta;
    private readonly DesktopPocDesignTokens _tokens = DesktopPocDesignTokens.Padrao;
    private readonly DesktopPocEffectsPolicy _politica;
    private readonly Panel _conteudo = new();
    private readonly Panel _rodape = new();
    private readonly Label _passo = new();
    private readonly Label _erro = new();
    private DesktopConfigurationDraft _rascunho;
    private DesktopToggle? _iniciarWindows;
    private DesktopTextField? _diretorio;
    private DesktopTextField? _enderecoObs;
    private DesktopTextField? _nomeCli;
    private DesktopTextField? _executavelCli;
    private DesktopActionButton? _avancar;

    public PrimeiroAcessoForm(
        ConfiguracaoAnamnesis configuracao,
        bool iniciarComWindows,
        TemaDesktopPoc tema,
        DesktopPocEffectsPolicy politica)
    {
        _original = configuracao;
        _rascunho = DesktopConfigurationDraft.From(configuracao, iniciarComWindows);
        _paleta = DesktopPocPalette.Criar(tema);
        _politica = politica;
        Configuracao = configuracao;
        Text = "Primeiro acesso | Anamnesis";
        AccessibleName = "Configuração inicial do Anamnesis";
        AccessibleDescription = "Wizard rápido de três passos para preparar o aplicativo.";
        Icon = IconeAnamnesis.Carregar();
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(760, 560);
        BackColor = _paleta.Superficies.Canvas;
        ForeColor = _paleta.Texto;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font(_tokens.Tipografia.Interface, _tokens.Tipografia.Corpo, FontStyle.Regular, GraphicsUnit.Point);

        _conteudo.Dock = DockStyle.Fill;
        _conteudo.Padding = new Padding(56, 40, 56, 22);
        _conteudo.BackColor = _paleta.Superficies.Canvas;
        _rodape.Dock = DockStyle.Bottom;
        _rodape.Height = 94;
        _rodape.Padding = new Padding(56, 18, 56, 20);
        _rodape.BackColor = _paleta.Superficies.Painel;
        _rodape.Paint += (_, e) =>
        {
            using var borda = new Pen(_paleta.Borda, 1F);
            e.Graphics.DrawLine(borda, 0, 0, _rodape.Width, 0);
        };
        _passo.AutoSize = true;
        _passo.Location = new Point(56, 39);
        _passo.ForeColor = _paleta.TextoSecundario;
        _passo.Font = new Font(_tokens.Tipografia.Interface, 9F, FontStyle.Bold, GraphicsUnit.Point);
        _rodape.Controls.Add(_passo);
        Controls.Add(_conteudo);
        Controls.Add(_rodape);
        RenderizarPasso();
    }

    public int TotalPassos { get; } = 3;

    public int PassoAtual { get; private set; } = 1;

    public ConfiguracaoAnamnesis Configuracao { get; private set; }

    public bool IniciarComWindows => _rascunho.IniciarComWindows;

    internal void AvancarAgora()
    {
        if (!CapturarPasso())
        {
            return;
        }

        if (PassoAtual < TotalPassos)
        {
            PassoAtual++;
            RenderizarPasso();
            return;
        }

        try
        {
            Configuracao = _rascunho.Build(_original);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (InvalidOperationException exception)
        {
            MostrarErro(exception.Message);
        }
    }

    private void VoltarAgora()
    {
        if (PassoAtual <= 1)
        {
            return;
        }

        PassoAtual--;
        RenderizarPasso();
    }

    private void RenderizarPasso()
    {
        _conteudo.SuspendLayout();
        _conteudo.Controls.Clear();
        _rodape.Controls.OfType<DesktopActionButton>().ToList().ForEach(botao => _rodape.Controls.Remove(botao));
        _erro.Text = string.Empty;
        _passo.Text = $"PASSO {PassoAtual} DE {TotalPassos}";

        switch (PassoAtual)
        {
            case 1:
                RenderizarBoasVindas();
                break;
            case 2:
                RenderizarCapturaEArquivo();
                break;
            default:
                RenderizarInteligencia();
                break;
        }

        var voltar = CriarBotao("Voltar", DesktopActionVariant.Ghost, DesktopActionIcon.ArrowLeft, (_, _) => VoltarAgora());
        voltar.Visible = PassoAtual > 1;
        voltar.Location = new Point(_rodape.Width - 306, 24);
        voltar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _avancar = CriarBotao(
            PassoAtual == TotalPassos ? "Concluir configuração" : "Continuar",
            DesktopActionVariant.Primary,
            PassoAtual == TotalPassos ? DesktopActionIcon.Check : DesktopActionIcon.ArrowRight,
            (_, _) => AvancarAgora());
        _avancar.Location = new Point(_rodape.Width - _avancar.Width - 56, 24);
        _avancar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _rodape.Controls.Add(voltar);
        _rodape.Controls.Add(_avancar);
        AcceptButton = _avancar;
        _conteudo.ResumeLayout(performLayout: true);
        AnimarEntradaPasso();
    }

    private void RenderizarBoasVindas()
    {
        var marca = new DesktopBrandMark(_paleta, _tokens)
        {
            Location = new Point(56, 0),
            Size = new Size(82, 82),
            AccessibleName = "Marca Anamnesis"
        };
        _conteudo.Controls.Add(marca);
        _conteudo.Controls.Add(CriarLabel("Bem-vindo ao Anamnesis", 22F, FontStyle.Bold, _paleta.Texto, new Point(56, 114)));
        _conteudo.Controls.Add(CriarLabel(
            "Gravação, transcrição e memória de reuniões no seu computador.",
            11F,
            FontStyle.Regular,
            _paleta.TextoSecundario,
            new Point(56, 158)));
        var cartao = CriarCartao(new Point(56, 220), new Size(648, 116));
        cartao.Controls.Add(CriarLabel("Abrir junto com o Windows", 11F, FontStyle.Bold, _paleta.Texto, new Point(20, 20)));
        cartao.Controls.Add(CriarLabel("Fica pronto na bandeja sem ocupar sua tela.", 9.5F, FontStyle.Regular, _paleta.TextoSecundario, new Point(20, 52)));
        _iniciarWindows = new DesktopToggle(_paleta, _tokens)
        {
            Checked = _rascunho.IniciarComWindows,
            Location = new Point(500, 37),
            AccessibleName = "Abrir Anamnesis junto com o Windows"
        };
        cartao.Controls.Add(_iniciarWindows);
        _conteudo.Controls.Add(cartao);
    }

    private void RenderizarCapturaEArquivo()
    {
        _conteudo.Controls.Add(CriarLabel("Capture e guarde do seu jeito", 22F, FontStyle.Bold, _paleta.Texto, new Point(56, 4)));
        _conteudo.Controls.Add(CriarLabel("Os valores recomendados já estão preenchidos e podem ser alterados depois.", 10F, FontStyle.Regular, _paleta.TextoSecundario, new Point(56, 48)));
        _diretorio = CriarCampo("Pasta das reuniões", "Pasta local para atas e transcrições", _rascunho.DiretorioArquivo, 100);
        _enderecoObs = CriarCampo("Endereço local do OBS", "Exemplo: ws://127.0.0.1:4455", _rascunho.EnderecoObs, 220);
    }

    private void RenderizarInteligencia()
    {
        _conteudo.Controls.Add(CriarLabel("Sua inteligência local", 22F, FontStyle.Bold, _paleta.Texto, new Point(56, 4)));
        _conteudo.Controls.Add(CriarLabel("O Anamnesis usa a CLI já autenticada e nunca envia gravações por conta própria.", 10F, FontStyle.Regular, _paleta.TextoSecundario, new Point(56, 48)));
        _nomeCli = CriarCampo("Nome da CLI", "Exemplo: Codex CLI", _rascunho.NomeCli, 100);
        _executavelCli = CriarCampo("Executável", "Caminho descoberto automaticamente", _rascunho.CaminhoExecutavelCli, 220);
        _erro.AutoSize = true;
        _erro.Location = new Point(56, 342);
        _erro.ForeColor = _paleta.Perigo;
        _erro.Font = new Font(_tokens.Tipografia.Interface, 9.5F, FontStyle.Bold, GraphicsUnit.Point);
        _erro.AccessibleRole = AccessibleRole.Alert;
        _conteudo.Controls.Add(_erro);
    }

    private DesktopTextField CriarCampo(
        string titulo,
        string ajuda,
        string valor,
        int y)
    {
        _conteudo.Controls.Add(CriarLabel(titulo, 10F, FontStyle.Bold, _paleta.Texto, new Point(56, y)));
        _conteudo.Controls.Add(CriarLabel(ajuda, 9F, FontStyle.Regular, _paleta.TextoSecundario, new Point(56, y + 28)));
        var campo = new DesktopTextField(_paleta, _tokens, ajuda)
        {
            Text = valor,
            Location = new Point(56, y + 54),
            Width = 648,
            AccessibleName = titulo,
            AccessibleDescription = ajuda
        };
        _conteudo.Controls.Add(campo);
        return campo;
    }

    private bool CapturarPasso()
    {
        try
        {
            if (PassoAtual == 1 && _iniciarWindows is not null)
            {
                _rascunho = _rascunho with { IniciarComWindows = _iniciarWindows.Checked };
            }
            else if (PassoAtual == 2 && _diretorio is not null && _enderecoObs is not null)
            {
                _rascunho = _rascunho with
                {
                    DiretorioArquivo = _diretorio.Text,
                    EnderecoObs = _enderecoObs.Text
                };
                _ = _rascunho.Build(_original);
            }
            else if (PassoAtual == 3 && _nomeCli is not null && _executavelCli is not null)
            {
                _rascunho = _rascunho with
                {
                    NomeCli = _nomeCli.Text,
                    CaminhoExecutavelCli = _executavelCli.Text
                };
            }

            return true;
        }
        catch (InvalidOperationException exception)
        {
            MostrarErro(exception.Message);
            return false;
        }
    }

    private void MostrarErro(string mensagem)
    {
        _erro.Text = mensagem;
        if (!_conteudo.Controls.Contains(_erro))
        {
            _erro.Location = new Point(56, 352);
            _erro.ForeColor = _paleta.Perigo;
            _erro.AccessibleRole = AccessibleRole.Alert;
            _conteudo.Controls.Add(_erro);
        }
    }

    private DesktopSurfacePanel CriarCartao(Point local, Size tamanho) =>
        new(_paleta, _tokens, _politica, DesktopSurfaceVariant.Elevated)
        {
            Location = local,
            Size = tamanho,
            CornerRadius = _tokens.Geometria.RaioMedio
        };

    private void AnimarEntradaPasso()
    {
        if (!_politica.AnimacoesAtivas || _conteudo.Controls.Count == 0)
        {
            return;
        }

        var destinos = _conteudo.Controls.Cast<Control>()
            .ToDictionary(control => control, control => control.Location);
        foreach (var (control, destino) in destinos)
        {
            control.Left = destino.X + 18;
        }

        var relogio = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            if (IsDisposed)
            {
                timer.Stop();
                timer.Dispose();
                return;
            }

            var progresso = relogio.Elapsed.TotalMilliseconds / _tokens.Motion.FastMs;
            foreach (var (control, destino) in destinos)
            {
                if (!control.IsDisposed)
                {
                    control.Left = destino.X + DesktopPocMotion.CalcularDeslocamento(progresso, 18);
                }
            }

            if (progresso < 1D)
            {
                return;
            }

            timer.Stop();
            timer.Dispose();
        };
        timer.Start();
    }

    private DesktopActionButton CriarBotao(
        string texto,
        DesktopActionVariant variante,
        DesktopActionIcon icon,
        EventHandler clique)
    {
        var botao = new DesktopActionButton(_paleta, _tokens, _politica, variante)
        {
            Text = texto,
            Icon = icon,
            AutoSize = true,
            MinimumSize = new Size(132, 42),
            Font = new Font(_tokens.Tipografia.Interface, 9.5F, FontStyle.Bold, GraphicsUnit.Point)
        };
        botao.Click += clique;
        return botao;
    }

    private Label CriarLabel(
        string texto,
        float tamanho,
        FontStyle estilo,
        Color cor,
        Point local) =>
        new()
        {
            Text = texto,
            AutoSize = true,
            Location = local,
            ForeColor = cor,
            BackColor = Color.Transparent,
            Font = new Font(
                estilo.HasFlag(FontStyle.Bold) ? _tokens.Tipografia.Display : _tokens.Tipografia.Interface,
                tamanho,
                estilo,
                GraphicsUnit.Point)
        };
}

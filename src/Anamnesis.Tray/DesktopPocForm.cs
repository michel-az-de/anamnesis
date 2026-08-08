using System.Drawing.Drawing2D;
using System.Globalization;
using System.Diagnostics;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.UseCases;
using Anamnesis.Infrastructure.Configuracao;

namespace Anamnesis.Tray;

internal sealed class DesktopPocForm : Form
{
    private static readonly string[] NiveisObservabilidade = ["Todos os níveis", "Info", "Aviso", "Erro"];
    private static readonly string[] IntervalosObservabilidade =
        ["Últimas 24 horas", "Última hora", "Últimos 7 dias", "Últimos 14 dias", "Todo o período"];

    private readonly DesktopPocPalette _paleta;
    private readonly TemaDesktopPoc _tema;
    private readonly DesktopPocDesignTokens _tokens;
    private readonly DesktopPocEffectsPolicy _politicaVisual;
    private readonly IDesktopSession _sessao;
    private readonly Func<Guid, string, DateTimeOffset, CancellationToken, Task>? _criarLembrete;
    private readonly Func<ConfiguracaoAnamnesis, CancellationToken, Task>? _salvarConfiguracao;
    private readonly Func<bool>? _obterInicializacaoWindows;
    private readonly Action<bool>? _definirInicializacaoWindows;
    private readonly DesktopPocObservabilityState _observabilidade;
    private readonly Panel _conteudo = new();
    private readonly Label _estadoGlobal = new();
    private readonly Dictionary<string, Button> _navegacao = [];
    private readonly System.Windows.Forms.Timer _timerGravacao = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _timerProcessamento = new() { Interval = 2800 };
    private readonly System.Windows.Forms.Timer _timerPolling = new() { Interval = 2000 };
    private readonly CancellationTokenSource _lifetime = new();
    private string _paginaAtual = "inicio";
    private Guid? _reuniaoDetalheId;
    private Guid? _reuniaoAcompanhadaId;
    private string? _filtroObservabilidade;
    private Panel? _areaListaReunioes;
    private Label? _resultadoReunioes;
    private int _versaoBuscaReunioes;
    private bool _pollingEmAndamento;
    private int _versaoNavegacao;
    private Label? _cronometro;
    private DesktopSignalMeter? _audioSistema;
    private DesktopSignalMeter? _microfone;
    private DesktopShadowPanel? _cartaoProcessamento;
    private Label? _tituloProcessamento;
    private Label? _descricaoProcessamento;
    private Label? _estadoProcessamento;
    private DesktopProgressBar? _barraProcessamento;
    private DesktopActionButton? _abrirLogsProcessamento;
    private DesktopActionButton? _abrirTranscricaoProcessamento;
    private Action? _atualizarConsoleObservabilidade;
    private int _onda;
    private ConfiguracaoAnamnesis? _configuracaoAtual;

    public DesktopPocForm()
        : this(
            DesktopPocTheme.ObterAtual(),
            DesktopPocSystemPreferences.Obter(),
            new DesktopDemoSession())
    {
    }

    internal DesktopPocForm(TemaDesktopPoc tema)
        : this(
            tema,
            new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
            new DesktopDemoSession())
    {
    }

    internal DesktopPocForm(TemaDesktopPoc tema, DesktopPocEffectsPolicy politicaVisual)
        : this(tema, politicaVisual, new DesktopDemoSession())
    {
    }

    internal DesktopPocForm(
        TemaDesktopPoc tema,
        DesktopPocEffectsPolicy politicaVisual,
        IDesktopSession sessao,
        Func<Guid, string, DateTimeOffset, CancellationToken, Task>? criarLembrete = null,
        Func<ConfiguracaoAnamnesis, CancellationToken, Task>? salvarConfiguracao = null,
        Func<bool>? obterInicializacaoWindows = null,
        Action<bool>? definirInicializacaoWindows = null)
    {
        _sessao = sessao ?? throw new ArgumentNullException(nameof(sessao));
        _criarLembrete = criarLembrete;
        _salvarConfiguracao = salvarConfiguracao;
        _obterInicializacaoWindows = obterInicializacaoWindows;
        _definirInicializacaoWindows = definirInicializacaoWindows;
        _configuracaoAtual = sessao.Ambiente?.Configuracao;
        _observabilidade = new DesktopPocObservabilityState(
            incluirHistorico: sessao.ModoDemonstracao);
        _tema = tema;
        _paleta = DesktopPocPalette.Criar(tema);
        _tokens = DesktopPocDesignTokens.Padrao;
        _politicaVisual = politicaVisual;
        Text = "Anamnesis";
        Icon = IconeAnamnesis.Carregar();
        BackColor = _paleta.Fundo;
        ForeColor = _paleta.Texto;
        Font = new Font(_tokens.Tipografia.Interface, _tokens.Tipografia.Corpo, FontStyle.Regular, GraphicsUnit.Point);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 640);
        ClientSize = new Size(1180, 760);
        AutoScaleMode = AutoScaleMode.Dpi;

        Controls.Add(CriarEstrutura());

        _timerGravacao.Tick += (_, _) => AtualizarGravacao();
        _timerProcessamento.Tick += (_, _) => ConcluirProcessamento();
        _timerPolling.Tick += async (_, _) => await AtualizarDadosReaisAsync();

        Navegar("inicio");
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowsTitleBarTheme.Aplicar(this, _tema, _paleta);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_sessao.ModoDemonstracao)
        {
            return;
        }

        await AtualizarDadosReaisAsync();
        _timerPolling.Start();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (_sessao.ModoDemonstracao)
        {
            return;
        }

        if (Visible)
        {
            _timerPolling.Start();
        }
        else
        {
            _timerPolling.Stop();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lifetime.Cancel();
            _timerGravacao.Dispose();
            _timerProcessamento.Dispose();
            _timerPolling.Dispose();
            _lifetime.Dispose();
        }

        base.Dispose(disposing);
    }

    private DesktopBackdropPanel CriarEstrutura()
    {
        var raiz = new DesktopBackdropPanel(_paleta, _tokens, _politicaVisual)
        {
            Dock = DockStyle.Fill
        };
        var topo = CriarTopo();
        var lateral = CriarBarraLateral();

        _conteudo.Dock = DockStyle.Fill;
        _conteudo.BackColor = _paleta.Superficies.Canvas;
        _conteudo.Padding = new Padding(_tokens.Espacamento.Xl, _tokens.Espacamento.Lg, _tokens.Espacamento.Xl, _tokens.Espacamento.Lg);

        raiz.Controls.Add(_conteudo);
        raiz.Controls.Add(lateral);
        raiz.Controls.Add(topo);
        return raiz;
    }

    private DesktopSurfacePanel CriarTopo()
    {
        var topo = new DesktopSurfacePanel(
            _paleta,
            _tokens,
            _politicaVisual,
            DesktopSurfaceVariant.Chrome)
        {
            Dock = DockStyle.Top,
            Height = 58,
            CornerRadius = 0,
            Padding = new Padding(_tokens.Espacamento.Lg, 10, _tokens.Espacamento.Xl, 10)
        };

        var marca = new DesktopBrandMark(_paleta, _tokens)
        {
            Dock = DockStyle.Left
        };

        var nome = new Label
        {
            Text = "Anamnesis",
            AutoSize = true,
            Font = new Font(_tokens.Tipografia.Display, 11F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = _paleta.Texto,
            Dock = DockStyle.Left,
            Padding = new Padding(10, 6, 0, 0)
        };

        _estadoGlobal.Text = "Tudo funcionando";
        _estadoGlobal.AutoSize = true;
        _estadoGlobal.ForeColor = _paleta.Positivo;
        _estadoGlobal.BackColor = _paleta.FundoPositivo;
        _estadoGlobal.Dock = DockStyle.Right;
        _estadoGlobal.Padding = new Padding(12, 7, 12, 7);
        AplicarRegiaoArredondada(_estadoGlobal, _tokens.Geometria.RaioMedio);

        var legenda = new Label
        {
            Text = _sessao.ModoDemonstracao ? "POC nativa do Windows" : "DADOS LOCAIS REAIS",
            AutoSize = true,
            ForeColor = _paleta.TextoSecundario,
            Dock = DockStyle.Right,
            Padding = new Padding(0, 7, 18, 0)
        };

        topo.Controls.Add(_estadoGlobal);
        topo.Controls.Add(legenda);
        topo.Controls.Add(nome);
        topo.Controls.Add(marca);
        return topo;
    }

    private DesktopSurfacePanel CriarBarraLateral()
    {
        var lateral = new DesktopSurfacePanel(
            _paleta,
            _tokens,
            _politicaVisual,
            DesktopSurfaceVariant.Navigation)
        {
            Dock = DockStyle.Left,
            Width = 196,
            CornerRadius = 0,
            Padding = new Padding(10, _tokens.Espacamento.Lg, 10, _tokens.Espacamento.Md)
        };

        var principal = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 306,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _paleta.Navegacao
        };

        principal.Controls.Add(CriarBotaoNavegacao("inicio", "Início", DesktopNavigationIcon.Home));
        principal.Controls.Add(CriarBotaoNavegacao("reunioes", "Reuniões", DesktopNavigationIcon.Meetings));
        principal.Controls.Add(CriarBotaoNavegacao("ao-vivo", "Ao vivo", DesktopNavigationIcon.Live));
        principal.Controls.Add(CriarBotaoNavegacao("tarefas", "Tarefas", DesktopNavigationIcon.Tasks));
        principal.Controls.Add(CriarBotaoNavegacao("atividade", "Atividade", DesktopNavigationIcon.Activity));
        principal.Controls.Add(CriarBotaoNavegacao("observabilidade", "Observabilidade", DesktopNavigationIcon.Observability));

        var configuracoes = CriarBotaoNavegacao("configuracoes", "Configurações", DesktopNavigationIcon.Settings);
        configuracoes.Dock = DockStyle.Bottom;

        lateral.Controls.Add(configuracoes);
        lateral.Controls.Add(principal);
        return lateral;
    }

    private DesktopNavigationButton CriarBotaoNavegacao(
        string pagina,
        string texto,
        DesktopNavigationIcon icon)
    {
        var botao = new DesktopNavigationButton(_paleta, _tokens, _politicaVisual, icon)
        {
            Text = texto,
            Width = 174,
            Height = 42,
            Margin = new Padding(0, 0, 2, 4),
            Font = new Font(_tokens.Tipografia.Interface, 9.5F, FontStyle.Regular, GraphicsUnit.Point)
        };
        botao.Click += (_, _) => Navegar(pagina);
        _navegacao[pagina] = botao;
        return botao;
    }

    private void Navegar(string pagina, string? filtroObservabilidade = null)
    {
        _versaoNavegacao++;
        _paginaAtual = pagina;
        _filtroObservabilidade = pagina == "observabilidade" ? filtroObservabilidade : null;
        if (pagina != "detalhe")
        {
            _reuniaoDetalheId = null;
        }
        foreach (var item in _navegacao)
        {
            if (item.Value is DesktopNavigationButton navegacao)
            {
                navegacao.DefinirSelecionado(item.Key == pagina);
            }
            else
            {
                item.Value.BackColor = item.Key == pagina ? _paleta.Selecao : _paleta.Navegacao;
                item.Value.ForeColor = _paleta.TextoNavegacao;
            }
        }

        LimparReferenciasDaPagina();

        var novaPagina = pagina switch
        {
            "reunioes" => CriarTelaReunioes(),
            "ao-vivo" => CriarTelaAoVivo(),
            "tarefas" => CriarTelaTarefas(),
            "atividade" => CriarTelaAtividade(),
            "observabilidade" => CriarTelaObservabilidade(),
            "configuracoes" => CriarTelaConfiguracoes(),
            _ => CriarTelaInicio()
        };

        _conteudo.SuspendLayout();
        foreach (var controle in _conteudo.Controls.Cast<Control>().ToArray())
        {
            _conteudo.Controls.Remove(controle);
            controle.Dispose();
        }
        novaPagina.Dock = DockStyle.Fill;
        _conteudo.Controls.Add(novaPagina);
        novaPagina.BringToFront();
        _conteudo.ResumeLayout(performLayout: true);
    }

    private void LimparReferenciasDaPagina()
    {
        _versaoBuscaReunioes++;
        _areaListaReunioes = null;
        _resultadoReunioes = null;
        _cronometro = null;
        _audioSistema = null;
        _microfone = null;
        _cartaoProcessamento = null;
        _tituloProcessamento = null;
        _descricaoProcessamento = null;
        _estadoProcessamento = null;
        _barraProcessamento = null;
        _abrirLogsProcessamento = null;
        _abrirTranscricaoProcessamento = null;
        _atualizarConsoleObservabilidade = null;
    }

    private Panel CriarTelaInicio()
    {
        var recuperacao = _sessao.RecuperacaoPendente;
        var gravando = _sessao.Etapa == EtapaDesktopPoc.Gravando;
        var textoAcao = recuperacao
            ? "Revisar gravação anterior"
            : gravando
                ? "Ver gravação ao vivo"
                : "Iniciar gravação";
        var iniciar = CriarBotaoPrimario(
            textoAcao,
            (_, _) =>
            {
                if (recuperacao || gravando)
                {
                    Navegar("ao-vivo");
                }
                else
                {
                    IniciarGravacao();
                }
            },
            recuperacao || gravando ? DesktopActionIcon.Live : DesktopActionIcon.Record);
        var pagina = CriarPagina(
            "Bom dia, Felipe",
            recuperacao
                ? "Uma gravação anterior aguarda sua decisão."
                : gravando
                    ? "A captura atual permanece visível e pode ser encerrada a qualquer momento."
                    : "Pronto para registrar sua próxima reunião.",
            iniciar,
            out var corpo);

        var status = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 102,
            ColumnCount = 3,
            Padding = new Padding(0, 0, 0, 16)
        };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
        if (_sessao.ModoDemonstracao)
        {
            status.Controls.Add(CriarCartaoStatus("OBS", "Pronto", "Captura universal"), 0, 0);
            status.Controls.Add(CriarCartaoStatus("Docker e Whisper", "Prontos", "Transcrição local"), 1, 0);
            status.Controls.Add(CriarCartaoStatus("Codex CLI", "Autenticado", "Ata estruturada"), 2, 0);
        }
        else
        {
            status.Controls.Add(CriarCartaoStatus("OBS", "Configurado", "Comando real pelo caso de uso"), 0, 0);
            status.Controls.Add(CriarCartaoStatus("Worker", "Local", "Processamento assíncrono"), 1, 0);
            status.Controls.Add(CriarCartaoStatus("SQLite", "Ativo", "Histórico persistido"), 2, 0);
        }

        var tituloLista = CriarCabecalhoSecao("Reuniões recentes", "Ver todas", (_, _) => Navegar("reunioes"));
        var lista = CriarListaReunioes(_sessao.Reunioes.Take(3));

        corpo.Controls.Add(lista);
        corpo.Controls.Add(tituloLista);
        corpo.Controls.Add(status);
        return pagina;
    }

    private Panel CriarTelaReunioes()
    {
        var pagina = CriarPagina(
            "Reuniões",
            _sessao.ModoDemonstracao
                ? $"{_sessao.Reunioes.Count} reuniões registradas nesta simulação"
                : $"{_sessao.Reunioes.Count} reuniões persistidas localmente",
            null,
            out var corpo);

        var barra = new Panel
        {
            Dock = DockStyle.Top,
            Height = 98,
            BackColor = _paleta.Superficies.Canvas,
            Padding = new Padding(0, 0, 0, 14)
        };
        barra.Controls.Add(new Label
        {
            Text = "BUSCAR E FILTRAR",
            AutoSize = true,
            ForeColor = _paleta.Destaque,
            Font = new Font(_tokens.Tipografia.Interface, 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(1, 0)
        });

        var campos = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = _paleta.Superficies.Canvas,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        campos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        campos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 192F));
        campos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 192F));
        campos.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var busca = CriarCampoTexto("Buscar em título, resumo, decisões, tarefas e transcrição");
        busca.Dock = DockStyle.Fill;
        busca.Margin = new Padding(0, 0, 12, 0);
        busca.AccessibleName = "Buscar reuniões";
        busca.AccessibleDescription = "Busca no título, resumo, decisões, tarefas e transcrição";

        var estadosDisponiveis = _sessao.Reunioes
            .Select(reuniao => reuniao.Status)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(statusReuniao => statusReuniao, StringComparer.Ordinal)
            .Prepend("Todos os estados")
            .ToArray();
        var estado = CriarCombo(estadosDisponiveis);
        estado.Dock = DockStyle.Fill;
        estado.Margin = Padding.Empty;
        estado.AccessibleName = "Filtrar por estado";

        var periodo = CriarCombo(["Todo o período", "Últimos 30 dias", "Esta semana", "Este ano"]);
        periodo.Dock = DockStyle.Fill;
        periodo.Margin = new Padding(0, 0, 12, 0);
        periodo.AccessibleName = "Filtrar por período";

        campos.Controls.Add(busca, 0, 0);
        campos.Controls.Add(periodo, 1, 0);
        campos.Controls.Add(estado, 2, 0);
        barra.Controls.Add(campos);

        var cabecalhoLista = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = _paleta.Superficies.Canvas
        };
        cabecalhoLista.Controls.Add(new Label
        {
            Text = "HISTÓRICO LOCAL",
            AutoSize = true,
            ForeColor = _paleta.Texto,
            Font = new Font(_tokens.Tipografia.Interface, 9F, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(1, 6)
        });
        _resultadoReunioes = new Label
        {
            AutoSize = true,
            ForeColor = _paleta.TextoSecundario,
            Font = new Font(_tokens.Tipografia.Interface, 9F, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(1, 31),
            AccessibleName = "Quantidade de reuniões encontradas"
        };
        cabecalhoLista.Controls.Add(_resultadoReunioes);

        var areaLista = new Panel { Dock = DockStyle.Fill, BackColor = _paleta.Superficies.Canvas };
        _areaListaReunioes = areaLista;
        void AtualizarListaLocal()
        {
            var filtro = busca.Text.Trim();
            var reunioes = _sessao.Reunioes.Where(reuniao =>
                (string.IsNullOrWhiteSpace(filtro) ||
                 reuniao.Titulo.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                 reuniao.Plataforma.Contains(filtro, StringComparison.OrdinalIgnoreCase)) &&
                (estado.SelectedIndex <= 0 || string.Equals(reuniao.Status, estado.Text, StringComparison.Ordinal)))
                .ToArray();
            areaLista.Controls.Clear();
            areaLista.Controls.Add(CriarListaReunioes(reunioes));
            AtualizarResumoReunioes(reunioes.Length);
        }

        async void SolicitarBusca(object? _, EventArgs __)
        {
            var versao = ++_versaoBuscaReunioes;
            try
            {
                await Task.Delay(280, _lifetime.Token);
                if (versao != _versaoBuscaReunioes)
                {
                    return;
                }

                await AtualizarListaReunioesAsync(busca.Text, estado.Text, periodo.Text, versao);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
        }

        busca.TextChanged += SolicitarBusca;
        estado.SelectedIndexChanged += SolicitarBusca;
        periodo.SelectedIndexChanged += SolicitarBusca;

        corpo.Controls.Add(areaLista);
        corpo.Controls.Add(cabecalhoLista);
        corpo.Controls.Add(barra);
        AtualizarListaLocal();
        return pagina;
    }

    private async Task AtualizarListaReunioesAsync(
        string? texto,
        string? status,
        string? periodo,
        int versao)
    {
        var resultados = await _sessao.BuscarReunioesAsync(
            texto,
            status,
            ResolverCriadaDesde(periodo),
            _lifetime.Token);
        if (IsDisposed || versao != _versaoBuscaReunioes || _areaListaReunioes is null)
        {
            return;
        }

        _areaListaReunioes.Controls.Clear();
        _areaListaReunioes.Controls.Add(CriarListaReunioes(resultados));
        AtualizarResumoReunioes(resultados.Count);
    }

    private void AtualizarResumoReunioes(int quantidade)
    {
        if (_resultadoReunioes is null || _resultadoReunioes.IsDisposed)
        {
            return;
        }

        _resultadoReunioes.Text = quantidade switch
        {
            0 => "Nenhuma reunião encontrada",
            1 => "1 reunião encontrada",
            _ => $"{quantidade} reuniões encontradas"
        };
    }

    private static DateTimeOffset? ResolverCriadaDesde(string? periodo)
    {
        var agora = DateTimeOffset.Now;
        return periodo switch
        {
            "Últimos 30 dias" => agora.AddDays(-30).ToUniversalTime(),
            "Esta semana" => new DateTimeOffset(
                    agora.Date.AddDays(-(((int)agora.DayOfWeek + 6) % 7)),
                    agora.Offset)
                .ToUniversalTime(),
            "Este ano" => new DateTimeOffset(agora.Year, 1, 1, 0, 0, 0, agora.Offset)
                .ToUniversalTime(),
            _ => null
        };
    }

    private Panel CriarTelaDetalhe(ReuniaoDesktopPoc reuniao, string abaInicial = "Resumo")
    {
        var voltar = CriarBotaoSecundario("←  Voltar", (_, _) => Navegar("reunioes"));
        var pagina = CriarPaginaDetalhe(reuniao, voltar, out var corpo);

        var abas = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 54,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = _paleta.Superficies.Canvas,
            Padding = new Padding(0, 4, 0, 5)
        };
        var detalhe = new Panel { Dock = DockStyle.Fill, BackColor = _paleta.Superficies.Canvas };
        var nomes = new[] { "Resumo", "Transcrição", "Decisões", "Tarefas", "Arquivos" };
        var botoes = new List<DesktopTabButton>();

        void Selecionar(string nome)
        {
            foreach (var botao in botoes)
            {
                botao.DefinirSelecionado(botao.Text == nome);
            }

            detalhe.Controls.Clear();
            detalhe.Controls.Add(CriarConteudoDetalhe(reuniao, nome));
        }

        foreach (var nome in nomes)
        {
            var icone = nome switch
            {
                "Resumo" => DesktopTabIcon.Resumo,
                "Transcrição" => DesktopTabIcon.Transcricao,
                "Decisões" => DesktopTabIcon.Decisoes,
                "Tarefas" => DesktopTabIcon.Tarefas,
                "Arquivos" => DesktopTabIcon.Arquivos,
                _ => DesktopTabIcon.Resumo
            };
            var botao = new DesktopTabButton(_paleta, _tokens, _politicaVisual, icone)
            {
                Text = nome,
                Margin = new Padding(0, 0, 14, 0),
                Font = new Font(_tokens.Tipografia.Interface, 9.5F, FontStyle.Bold, GraphicsUnit.Point),
                AccessibleName = $"Aba {nome}"
            };
            botao.Click += (_, _) => Selecionar(nome);
            botoes.Add(botao);
            abas.Controls.Add(botao);
        }

        corpo.Controls.Add(detalhe);
        corpo.Controls.Add(abas);
        Selecionar(nomes.Contains(abaInicial, StringComparer.Ordinal) ? abaInicial : "Resumo");
        return pagina;
    }

    private FlowLayoutPanel CriarConteudoDetalhe(ReuniaoDesktopPoc reuniao, string aba)
    {
        var rolagem = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _paleta.Superficies.Canvas,
            Padding = new Padding(0, 18, 10, 10)
        };
        rolagem.SizeChanged += (_, _) => AjustarLarguraFilhos(rolagem);

        rolagem.Controls.Add(CriarIntroducaoDetalhe(aba, reuniao));

        switch (aba)
        {
            case "Transcrição":
                rolagem.Controls.Add(CriarBlocoTexto("Conteúdo transcrito", reuniao.Transcricao));
                break;
            case "Decisões":
                rolagem.Controls.Add(CriarBlocoTexto("Decisões registradas", reuniao.Decisoes.Select(item => $"•  {item}")));
                break;
            case "Tarefas":
                rolagem.Controls.Add(CriarBlocoTexto("Itens de ação", reuniao.Tarefas.Select(item => $"□  {item}")));
                if (_criarLembrete is not null && reuniao.Tarefas.Count > 0)
                {
                    rolagem.Controls.Add(CriarBlocoLembretes(reuniao.Id, reuniao.Tarefas));
                }
                break;
            case "Arquivos":
                if (_sessao.ModoDemonstracao)
                {
                    rolagem.Controls.Add(CriarBlocoTexto(
                        "Arquivos simulados",
                        ["ata.md  •  Abrir", "transcricao.md  •  Abrir", "gravacao.mp4  •  Mostrar no Explorer"]));
                }
                else
                {
                    rolagem.Controls.Add(CriarBlocoArquivosPersistidos(reuniao));
                }
                break;
            default:
                rolagem.Controls.Add(CriarBlocoTexto("Principais pontos", reuniao.PontosPrincipais.Select(item => $"•  {item}")));
                rolagem.Controls.Add(CriarBlocoTexto("Resumo executivo", [reuniao.Resumo]));
                break;
        }

        AjustarLarguraFilhos(rolagem);
        return rolagem;
    }

    private Panel CriarPaginaDetalhe(ReuniaoDesktopPoc reuniao, Control voltar, out Panel corpo)
    {
        var pagina = new Panel { Dock = DockStyle.Fill, BackColor = _paleta.Superficies.Canvas };
        var cabecalho = new Panel
        {
            Dock = DockStyle.Top,
            Height = 94,
            BackColor = _paleta.Superficies.Canvas
        };
        var contexto = new Label
        {
            Text = "DETALHES DA REUNIÃO",
            AutoSize = true,
            ForeColor = _paleta.Destaque,
            Font = new Font(_tokens.Tipografia.Interface, 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(2, 0)
        };
        var titulo = new Label
        {
            Text = reuniao.Titulo,
            AutoSize = true,
            MaximumSize = new Size(680, 32),
            AutoEllipsis = true,
            ForeColor = _paleta.Texto,
            Font = new Font(_tokens.Tipografia.Display, 21F, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(0, 20),
            AccessibleName = $"Reunião: {reuniao.Titulo}"
        };
        var metadadosTexto = $"{reuniao.Data}   •   {reuniao.Duracao}   •   {reuniao.Plataforma}";
        var metadados = new Label
        {
            Text = metadadosTexto,
            AutoSize = true,
            ForeColor = _paleta.TextoSecundario,
            Font = new Font(_tokens.Tipografia.Interface, 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(2, 61)
        };
        var larguraMetadados = TextRenderer.MeasureText(metadadosTexto, metadados.Font).Width;
        var status = new Label
        {
            Text = reuniao.Status,
            AutoSize = true,
            ForeColor = _paleta.Positivo,
            BackColor = _paleta.FundoPositivo,
            Font = new Font(_tokens.Tipografia.Interface, 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            Padding = new Padding(8, 3, 8, 3),
            Location = new Point(Math.Min(560, larguraMetadados + 18), 57),
            AccessibleName = $"Estado da reunião: {reuniao.Status}"
        };
        AplicarRegiaoArredondada(status, _tokens.Geometria.RaioPequeno);

        voltar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        voltar.Location = new Point(cabecalho.Width - voltar.Width, 2);
        cabecalho.Resize += (_, _) => voltar.Left = cabecalho.ClientSize.Width - voltar.Width;

        cabecalho.Controls.Add(voltar);
        cabecalho.Controls.Add(status);
        cabecalho.Controls.Add(metadados);
        cabecalho.Controls.Add(titulo);
        cabecalho.Controls.Add(contexto);
        corpo = new Panel { Dock = DockStyle.Fill, BackColor = _paleta.Superficies.Canvas };
        pagina.Controls.Add(corpo);
        pagina.Controls.Add(cabecalho);
        return pagina;
    }

    private Panel CriarIntroducaoDetalhe(string aba, ReuniaoDesktopPoc reuniao)
    {
        var (titulo, descricao) = aba switch
        {
            "Transcrição" => ("Transcrição completa", "Registro local da conversa, disponível para seleção e cópia."),
            "Decisões" => ("O que ficou decidido", $"{reuniao.Decisoes.Count} {Pluralizar(reuniao.Decisoes.Count, "decisão consolidada", "decisões consolidadas")} nesta reunião."),
            "Tarefas" => ("Próximas ações", $"{reuniao.Tarefas.Count} {Pluralizar(reuniao.Tarefas.Count, "tarefa registrada", "tarefas registradas")} para acompanhamento."),
            "Arquivos" => ("Arquivos e exportação", "Abra os artefatos locais ou gere uma cópia da ata."),
            _ => ("Visão geral", "Síntese da reunião e principais pontos registrados localmente.")
        };
        var painel = new Panel
        {
            Height = 66,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = _paleta.Superficies.Canvas
        };
        painel.Controls.Add(new Label
        {
            Text = descricao,
            AutoSize = true,
            ForeColor = _paleta.TextoSecundario,
            Font = new Font(_tokens.Tipografia.Interface, 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(1, 36)
        });
        painel.Controls.Add(new Label
        {
            Text = titulo,
            AutoSize = true,
            ForeColor = _paleta.Texto,
            Font = new Font(_tokens.Tipografia.Display, 15F, FontStyle.Bold, GraphicsUnit.Point),
            Location = Point.Empty
        });
        return painel;
    }

    private static string Pluralizar(int quantidade, string singular, string plural) =>
        quantidade == 1 ? singular : plural;

    private DesktopSurfacePanel CriarBlocoArquivosPersistidos(ReuniaoDesktopPoc reuniao)
    {
        var bloco = CriarCartao(DesktopSurfaceVariant.Base);
        bloco.Height = 342;
        bloco.Padding = new Padding(20);
        bloco.Controls.Add(CriarLabel(
            "Arquivos persistidos",
            12F,
            _paleta.Texto,
            new Point(20, 16),
            FontStyle.Bold));

        var exportarPdf = CriarBotaoSecundario(
            "Exportar PDF",
            async (_, _) => await SolicitarExportacaoAtaAsync(reuniao, FormatoExportacaoAta.Pdf));
        exportarPdf.Location = new Point(20, 54);
        exportarPdf.Size = new Size(150, 40);
        var exportarDocx = CriarBotaoSecundario(
            "Exportar DOCX",
            async (_, _) => await SolicitarExportacaoAtaAsync(reuniao, FormatoExportacaoAta.Docx));
        exportarDocx.Location = new Point(182, 54);
        exportarDocx.Size = new Size(160, 40);
        var publicarObsidian = CriarBotaoSecundario(
            "Publicar no Obsidian",
            async (_, _) => await SolicitarPublicacaoObsidianAsync(reuniao));
        publicarObsidian.Location = new Point(354, 54);
        publicarObsidian.Size = new Size(190, 40);
        bloco.Controls.Add(publicarObsidian);
        bloco.Controls.Add(exportarDocx);
        bloco.Controls.Add(exportarPdf);

        var topo = 118;
        AdicionarArtefato(
            bloco,
            "Ata",
            reuniao.CaminhoAta,
            "Abrir ata",
            caminho => _sessao.AbrirArquivoAsync(caminho, _lifetime.Token),
            ref topo);
        AdicionarArtefato(
            bloco,
            "Transcrição",
            reuniao.CaminhoTranscricao,
            "Abrir texto",
            caminho => _sessao.AbrirArquivoAsync(caminho, _lifetime.Token),
            ref topo);
        AdicionarArtefato(
            bloco,
            "Gravação",
            reuniao.CaminhoGravacao,
            "Mostrar arquivo",
            caminho => _sessao.MostrarNaPastaAsync(caminho, _lifetime.Token),
            ref topo);
        AdicionarArtefato(
            bloco,
            "Pasta arquivada",
            reuniao.DiretorioArtefatos,
            "Abrir pasta",
            caminho => _sessao.MostrarNaPastaAsync(caminho, _lifetime.Token),
            ref topo);
        return bloco;
    }

    private void AdicionarArtefato(
        Control bloco,
        string rotulo,
        string? caminho,
        string textoBotao,
        Func<string, Task> executar,
        ref int topo)
    {
        var caminhoVisivel = string.IsNullOrWhiteSpace(caminho) ? "Não disponível" : caminho;
        var label = new Label
        {
            Text = $"{rotulo}: {caminhoVisivel}",
            ForeColor = string.IsNullOrWhiteSpace(caminho) ? _paleta.TextoSecundario : _paleta.Texto,
            AutoEllipsis = true,
            Location = new Point(20, topo + 11),
            Size = new Size(560, 24)
        };
        bloco.Controls.Add(label);
        if (!string.IsNullOrWhiteSpace(caminho))
        {
            var caminhoPersistido = caminho;
            var botao = CriarBotaoSecundario(
                textoBotao,
                async (_, _) => await ExecutarAcaoArtefatoAsync(() => executar(caminhoPersistido)));
            botao.Location = new Point(600, topo);
            botao.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bloco.Controls.Add(botao);
            bloco.Resize += (_, _) => botao.Left = bloco.ClientSize.Width - botao.Width - 20;
            bloco.Resize += (_, _) => label.Width = Math.Max(120, botao.Left - 32);
        }

        topo += 48;
    }

    private async Task ExecutarAcaoArtefatoAsync(Func<Task> acao)
    {
        try
        {
            await acao();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            MostrarFalhaSegura(
                $"Não foi possível abrir o artefato. {exception.Message}",
                "Artefato indisponível",
                exception,
                "abrir_artefato");
        }
    }

    private async Task SolicitarExportacaoAtaAsync(
        ReuniaoDesktopPoc reuniao,
        FormatoExportacaoAta formato)
    {
        var extensao = formato == FormatoExportacaoAta.Pdf ? "pdf" : "docx";
        using var dialogo = new SaveFileDialog
        {
            Title = $"Exportar ata em {extensao.ToUpperInvariant()}",
            Filter = formato == FormatoExportacaoAta.Pdf
                ? "Documento PDF (*.pdf)|*.pdf"
                : "Documento Word (*.docx)|*.docx",
            DefaultExt = extensao,
            AddExtension = true,
            OverwritePrompt = true,
            FileName = $"ata-{CriarNomeArquivoSeguro(reuniao.Titulo)}.{extensao}"
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var caminho = await ExportarAtaAgoraAsync(
                reuniao.Id,
                formato,
                dialogo.FileName,
                File.Exists(dialogo.FileName));
            MessageBox.Show(
                $"Ata exportada com sucesso em:\n{caminho}",
                "Exportação concluída",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MostrarFalhaSegura(
                $"Não foi possível exportar a ata. {exception.Message}",
                "Falha na exportação",
                exception,
                "exportar_ata");
        }
    }

    private async Task SolicitarPublicacaoObsidianAsync(ReuniaoDesktopPoc reuniao)
    {
        using var dialogo = new FolderBrowserDialog
        {
            Description = "Escolha a pasta raiz do vault Obsidian. Ela precisa conter .obsidian.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (CaminhoPossivelmenteSincronizado(dialogo.SelectedPath) &&
            MessageBox.Show(
                "Este vault parece estar em uma pasta sincronizada. A nota publicada terá ciclo de vida independente da retenção do Anamnesis. Deseja continuar?",
                "Vault possivelmente sincronizado",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var caminho = await PublicarAtaObsidianAgoraAsync(
                reuniao.Id,
                dialogo.SelectedPath,
                "Anamnesis/Reunioes");
            MessageBox.Show(
                $"Ata publicada com sucesso em:\n{caminho}",
                "Publicação concluída",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MostrarFalhaSegura(
                $"Não foi possível publicar a ata. {exception.Message}",
                "Falha na publicação",
                exception,
                "publicar_ata_obsidian");
        }
    }

    private static string CriarNomeArquivoSeguro(string titulo)
    {
        var invalido = Path.GetInvalidFileNameChars();
        var normalizado = new string(titulo.Trim().Select(caractere =>
            invalido.Contains(caractere) || char.IsWhiteSpace(caractere) ? '-' : caractere).ToArray());
        while (normalizado.Contains("--", StringComparison.Ordinal))
        {
            normalizado = normalizado.Replace("--", "-", StringComparison.Ordinal);
        }

        normalizado = normalizado.Trim('-', '.');
        if (normalizado.Length == 0)
        {
            normalizado = "reuniao";
        }

        return normalizado.Length <= 72 ? normalizado : normalizado[..72].TrimEnd('-');
    }

    private static bool CaminhoPossivelmenteSincronizado(string caminho) =>
        caminho.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) ||
        caminho.Contains("Dropbox", StringComparison.OrdinalIgnoreCase) ||
        caminho.Contains("Google Drive", StringComparison.OrdinalIgnoreCase);

    private Panel CriarTelaAoVivo()
    {
        var recuperacao = _sessao.RecuperacaoPendente;
        var subtitulo = recuperacao
            ? "Recuperação pendente • nenhuma ação automática foi enviada ao OBS"
            : _sessao.Etapa == EtapaDesktopPoc.Gravando
            ? _sessao.ModoDemonstracao
                ? "Google Meet detectado  •  áudio e microfone ativos"
                : "Captura OBS ativa e reunião persistida no SQLite"
            : "Nenhuma reunião sendo gravada";
        var pagina = CriarPagina("Ao vivo", subtitulo, null, out var corpo);

        if (_sessao.Etapa != EtapaDesktopPoc.Gravando)
        {
            var cartao = new DesktopShadowPanel(_paleta, _tokens, _politicaVisual, DesktopSurfaceVariant.Elevated)
            {
                Dock = DockStyle.Top,
                Height = 220,
                Padding = new Padding(28),
                CornerRadius = _tokens.Geometria.RaioMedio
            };
            var iniciar = CriarBotaoPrimario("Iniciar gravação", (_, _) => IniciarGravacao(), DesktopActionIcon.Record);
            iniciar.Location = new Point(28, 142);
            cartao.Controls.Add(iniciar);
            cartao.Controls.Add(CriarLabel("O Anamnesis iniciará OBS e Docker automaticamente no fluxo real.", 10F, _paleta.TextoSecundario, new Point(28, 92)));
            cartao.Controls.Add(CriarLabel("Pronto para gravar", 18F, _paleta.Texto, new Point(28, 30), FontStyle.Bold));
            corpo.Controls.Add(cartao);
            return pagina;
        }

        var vivo = new DesktopShadowPanel(_paleta, _tokens, _politicaVisual, DesktopSurfaceVariant.Elevated)
        {
            Dock = DockStyle.Top,
            Height = 420,
            Padding = new Padding(30),
            CornerRadius = _tokens.Geometria.RaioMedio,
            AccentColor = recuperacao ? _paleta.Destaque : _paleta.Perigo
        };

        var gravando = CriarLabel(
            recuperacao ? "RECUPERAÇÃO PENDENTE" : "GRAVANDO AGORA",
            10F,
            recuperacao ? _paleta.Destaque : _paleta.Perigo,
            new Point(30, 28),
            FontStyle.Bold);
        _cronometro = CriarLabel(FormatarCronometro(), 34F, _paleta.Texto, new Point(30, 66), FontStyle.Bold);
        _cronometro.AutoSize = true;
        var reuniaoAtiva = _sessao.Reunioes.FirstOrDefault(reuniao => reuniao.Status == "Gravando");
        var plataforma = CriarLabel(
            _sessao.ModoDemonstracao
                ? "Google Meet  •  Reunião sem título"
                : $"Captura OBS  •  {reuniaoAtiva?.Titulo ?? "Reunião sem título"}",
            11F,
            _paleta.TextoSecundario,
            new Point(30, 140));
        vivo.Controls.Add(plataforma);

        var encerrar = CriarBotaoPerigo(
            recuperacao ? "Encerrar gravação anterior" : "Encerrar e transcrever",
            (_, _) => EncerrarGravacao(),
            DesktopActionIcon.Stop);
        if (_sessao.ModoDemonstracao)
        {
            var sistemaTexto = CriarLabel("Áudio do sistema", 10F, _paleta.Texto, new Point(30, 180));
            _audioSistema = new DesktopSignalMeter(_paleta, _tokens, _politicaVisual, _paleta.Destaque)
            {
                Location = new Point(30, 210),
                Size = new Size(520, 15),
                Value = 54
            };
            var micTexto = CriarLabel("Microfone", 10F, _paleta.Texto, new Point(30, 246));
            _microfone = new DesktopSignalMeter(_paleta, _tokens, _politicaVisual, _paleta.Positivo)
            {
                Location = new Point(30, 276),
                Size = new Size(520, 15),
                Value = 36
            };
            encerrar.Location = new Point(30, 330);
            vivo.Controls.Add(_microfone);
            vivo.Controls.Add(micTexto);
            vivo.Controls.Add(_audioSistema);
            vivo.Controls.Add(sistemaTexto);
        }
        else
        {
            vivo.Controls.Add(CriarLabel(
                recuperacao
                    ? "Nenhum comando foi enviado ao OBS neste reinício. Encerrar é uma ação explícita e será registrada."
                    : "A captura foi iniciada pelo caso de uso e está protegida pela trava de gravação única.",
                10F,
                _paleta.TextoSecundario,
                new Point(30, 190)));
            encerrar.Location = new Point(30, 248);
        }

        vivo.Controls.Add(encerrar);
        vivo.Controls.Add(_cronometro);
        vivo.Controls.Add(gravando);
        corpo.Controls.Add(vivo);
        return pagina;
    }

    private Panel CriarTelaTarefas()
    {
        var pagina = CriarPagina("Tarefas e decisões", "Itens extraídos das atas recentes", null, out var corpo);
        if (_sessao.ModoDemonstracao)
        {
            corpo.Controls.Add(CriarListaInformativa(
            [
                ("Validar POC visual", "Planejamento do produto  •  Felipe", "Hoje"),
                ("Criar shell do Desktop", "Planejamento do produto  •  Anamnesis", "Próxima SPEK"),
                ("Revisar política de retenção", "Revisão da arquitetura  •  Felipe", "12 ago")
            ]));
        }
        else
        {
            corpo.Controls.Add(CriarAviso(
                "Abra uma reunião arquivada para consultar tarefas e decisões reais extraídas da ata."));
        }

        return pagina;
    }

    private Panel CriarTelaAtividade()
    {
        var pagina = CriarPagina("Atividade", "Processamento local e eventos operacionais", null, out var corpo);
        var reuniaoAcompanhada = ObterReuniaoAcompanhada();
        var itens = new List<(string, string, string)>();

        if (_sessao.ModoDemonstracao)
        {
            itens.Add(("Planejamento do produto", "Ata gerada e arquivos arquivados", "Concluído"));
            itens.Add(("Diagnóstico automático", "OBS, Docker, Whisper e Codex disponíveis", "Saudável"));
        }
        else
        {
            itens.AddRange(_sessao.Reunioes
                .Where(reuniao => reuniaoAcompanhada is null || reuniao.Id != reuniaoAcompanhada.Id)
                .Take(20)
                .Select(reuniao => (
                    reuniao.Titulo,
                    reuniao.MotivoFalha ?? reuniao.Resumo,
                    reuniao.Status)));
        }

        var lista = CriarListaInformativa(itens);
        corpo.Controls.Add(lista);

        if (_sessao.Etapa is EtapaDesktopPoc.Processando or EtapaDesktopPoc.Concluido)
        {
            corpo.Controls.Add(CriarCartaoAcompanhamentoProcessamento());
        }

        return pagina;
    }

    private DesktopShadowPanel CriarCartaoAcompanhamentoProcessamento()
    {
        _cartaoProcessamento = new DesktopShadowPanel(_paleta, _tokens, _politicaVisual, DesktopSurfaceVariant.Elevated)
        {
            Dock = DockStyle.Top,
            Height = 194,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(20),
            CornerRadius = _tokens.Geometria.RaioMedio,
            AccentColor = _paleta.Destaque
        };

        _estadoProcessamento = CriarLabel(
            "PROCESSAMENTO LOCAL",
            8.5F,
            _paleta.Destaque,
            new Point(20, 16),
            FontStyle.Bold);
        _estadoProcessamento.Font = new Font(
            _tokens.Tipografia.Mono,
            8.5F,
            FontStyle.Bold,
            GraphicsUnit.Point);
        _tituloProcessamento = CriarLabel("", 13F, _paleta.Texto, new Point(20, 42), FontStyle.Bold);
        _descricaoProcessamento = CriarLabel("", 9.5F, _paleta.TextoSecundario, new Point(20, 70));
        _barraProcessamento = new DesktopProgressBar(_paleta, _tokens, _politicaVisual)
        {
            Location = new Point(20, 100),
            MarqueeAtivo = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _cartaoProcessamento.Resize += (_, _) =>
        {
            if (_barraProcessamento is not null)
            {
                _barraProcessamento.Width = Math.Max(160, _cartaoProcessamento.ClientSize.Width - 40);
            }
        };
        _abrirLogsProcessamento = CriarBotaoSecundario("Ver console de logs", (_, _) =>
        {
            var reuniao = ObterReuniaoAcompanhada();
            if (reuniao is not null)
            {
                AbrirConsoleDaReuniao(reuniao.Id);
            }
        });
        _abrirLogsProcessamento.Location = new Point(20, 134);
        _abrirTranscricaoProcessamento = CriarBotaoPrimario("Abrir transcrição", (_, _) =>
        {
            var reuniao = ObterReuniaoAcompanhada();
            if (reuniao is not null)
            {
                AbrirTranscricaoDaReuniao(reuniao.Id);
            }
        });
        _abrirTranscricaoProcessamento.Location = new Point(170, 134);

        _cartaoProcessamento.Controls.Add(_abrirTranscricaoProcessamento);
        _cartaoProcessamento.Controls.Add(_abrirLogsProcessamento);
        _cartaoProcessamento.Controls.Add(_barraProcessamento);
        _cartaoProcessamento.Controls.Add(_descricaoProcessamento);
        _cartaoProcessamento.Controls.Add(_tituloProcessamento);
        _cartaoProcessamento.Controls.Add(_estadoProcessamento);
        AtualizarAcompanhamentoProcessamento();
        return _cartaoProcessamento;
    }

    private void AtualizarAcompanhamentoProcessamento()
    {
        if (_cartaoProcessamento is null ||
            _cartaoProcessamento.IsDisposed ||
            _tituloProcessamento is null ||
            _descricaoProcessamento is null ||
            _estadoProcessamento is null ||
            _barraProcessamento is null ||
            _abrirLogsProcessamento is null ||
            _abrirTranscricaoProcessamento is null)
        {
            return;
        }

        var reuniao = ObterReuniaoAcompanhada();
        var emProcessamento = _sessao.Etapa == EtapaDesktopPoc.Processando;
        var concluido = _sessao.Etapa == EtapaDesktopPoc.Concluido && reuniao is not null;
        _cartaoProcessamento.Visible = emProcessamento || concluido;
        if (!_cartaoProcessamento.Visible)
        {
            return;
        }

        var titulo = reuniao?.Titulo ?? "Reunião em processamento";
        _estadoProcessamento.Text = concluido ? "PROCESSAMENTO CONCLUÍDO" : "PROCESSAMENTO LOCAL";
        _estadoProcessamento.ForeColor = concluido ? _paleta.Positivo : _paleta.Destaque;
        _tituloProcessamento.Text = concluido ? $"Concluída: {titulo}" : $"Em processamento: {titulo}";
        _descricaoProcessamento.Text = concluido
            ? "Transcrição e ata concluídas. Abra a reunião diretamente na aba de transcrição."
            : DescreverProcessamento(reuniao);
        _barraProcessamento.Visible = emProcessamento;
        _barraProcessamento.MarqueeAtivo = emProcessamento;
        _abrirLogsProcessamento.Visible = reuniao is not null;
        _abrirTranscricaoProcessamento.Visible = concluido;
    }

    private string DescreverProcessamento(ReuniaoDesktopPoc? reuniao) => reuniao?.Status switch
    {
        "Aguardando processamento" => "Gravação salva. Aguardando o Worker local iniciar.",
        "Transcrevendo" => "Whisper transcrevendo localmente.",
        "Gerando ata" => "Gerando a ata estruturada localmente.",
        "Arquivando" => "Arquivando os artefatos locais da reunião.",
        _ => _sessao.ModoDemonstracao
            ? "Gravação salva. Whisper iniciou a transcrição local simulada."
            : "Gravação salva. O Worker iniciou o processamento local."
    };

    private ReuniaoDesktopPoc? ObterReuniaoAcompanhada()
    {
        var reuniaoId = _sessao.ReuniaoAcompanhadaId ?? _reuniaoAcompanhadaId;
        if (reuniaoId is not null)
        {
            var reuniao = _sessao.Reunioes.FirstOrDefault(item => item.Id == reuniaoId.Value);
            if (reuniao is not null)
            {
                _reuniaoAcompanhadaId = reuniao.Id;
                return reuniao;
            }
        }

        var emProcessamento = _sessao.Reunioes.FirstOrDefault(reuniao =>
            reuniao.Status is "Aguardando processamento" or "Transcrevendo" or "Gerando ata" or "Arquivando");
        if (emProcessamento is not null)
        {
            _reuniaoAcompanhadaId = emProcessamento.Id;
        }

        return emProcessamento;
    }

    private void AtualizarReuniaoAcompanhada()
    {
        _ = ObterReuniaoAcompanhada();
    }

    private void AbrirConsoleDaReuniao(Guid reuniaoId) =>
        Navegar("observabilidade", $"r:{reuniaoId:N}");

    private async void AbrirTranscricaoDaReuniao(Guid reuniaoId) =>
        await AbrirDetalheAsync(reuniaoId, "Transcrição");

    private Panel CriarTelaObservabilidade()
    {
        var pagina = CriarPagina(
            "Observabilidade",
            _sessao.ModoDemonstracao
                ? "Eventos operacionais, correlação e tempos do fluxo local"
                : "Últimos 500 eventos locais persistidos, correlação e tempos do fluxo real",
            null,
            out var corpo);

        var faixa = new DesktopSurfacePanel(
            _paleta,
            _tokens,
            _politicaVisual,
            DesktopSurfaceVariant.Base)
        {
            Dock = DockStyle.Top,
            Height = 50,
            CornerRadius = _tokens.Geometria.RaioMedio,
            AccentColor = _paleta.Positivo,
            Padding = new Padding(14, 8, 14, 8)
        };
        var modo = new Label
        {
            Text = _sessao.ModoDemonstracao ? "TELEMETRIA SIMULADA" : "EVENTOS LOCAIS PERSISTIDOS",
            AutoSize = true,
            ForeColor = _paleta.Positivo,
            Font = new Font(_tokens.Tipografia.Mono, _tokens.Tipografia.Dado, FontStyle.Bold, GraphicsUnit.Point),
            Dock = DockStyle.Left,
            Padding = new Padding(0, 7, 18, 0)
        };
        var privacidade = new Label
        {
            Text = "Sem áudio, transcrição, prompts, segredos ou caminhos pessoais",
            AutoSize = true,
            ForeColor = _paleta.TextoSecundario,
            Dock = DockStyle.Left,
            Padding = new Padding(0, 7, 0, 0)
        };
        var aoVivo = new Label
        {
            Text = "AO VIVO",
            AutoSize = true,
            ForeColor = _paleta.Positivo,
            Font = new Font(_tokens.Tipografia.Mono, _tokens.Tipografia.Dado, FontStyle.Bold, GraphicsUnit.Point),
            Dock = DockStyle.Right,
            Padding = new Padding(0, 7, 0, 0)
        };
        faixa.Controls.Add(aoVivo);
        faixa.Controls.Add(privacidade);
        faixa.Controls.Add(modo);

        var metricas = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 112,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = _paleta.Superficies.Canvas,
            Padding = new Padding(0, 10, 0, 12)
        };
        for (var coluna = 0; coluna < 4; coluna++)
        {
            metricas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        metricas.Controls.Add(CriarCartaoMetricaObservabilidade("EVENTOS", "Eventos visíveis", _paleta.Texto, out var totalEventos), 0, 0);
        metricas.Controls.Add(CriarCartaoMetricaObservabilidade("ALERTAS", "Avisos e erros", _paleta.Destaque, out var totalAlertas), 1, 0);
        metricas.Controls.Add(CriarCartaoMetricaObservabilidade("TEMPO MÉDIO", "Operações medidas", _paleta.Positivo, out var duracaoMedia), 2, 0);
        metricas.Controls.Add(CriarCartaoMetricaObservabilidade("FILA", "Jobs não concluídos", _paleta.Texto, out var jobsNaFila), 3, 0);

        var filtros = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 54,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = _paleta.Superficies.Canvas,
            Padding = new Padding(0, 2, 0, 10)
        };
        filtros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        filtros.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176F));
        filtros.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176F));
        filtros.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));

        var busca = CriarCampoTexto("Filtrar evento, mensagem ou correlação");
        busca.Dock = DockStyle.Fill;
        busca.Margin = new Padding(0, 2, 10, 2);
        busca.Text = _filtroObservabilidade ?? string.Empty;
        var nivel = CriarCombo(NiveisObservabilidade);
        nivel.Dock = DockStyle.Fill;
        nivel.Margin = new Padding(0, 2, 10, 2);
        var intervalo = CriarCombo(IntervalosObservabilidade);
        intervalo.Dock = DockStyle.Fill;
        intervalo.Margin = new Padding(0, 2, 10, 2);
        var componentes = _observabilidade.Componentes.Prepend("Todos os componentes").ToArray();
        var componente = CriarCombo(componentes);
        componente.Dock = DockStyle.Fill;
        componente.Margin = new Padding(0, 2, 0, 2);
        filtros.Controls.Add(busca, 0, 0);
        filtros.Controls.Add(nivel, 1, 0);
        filtros.Controls.Add(intervalo, 2, 0);
        filtros.Controls.Add(componente, 3, 0);

        var console = new DesktopSurfacePanel(
            _paleta,
            _tokens,
            _politicaVisual,
            DesktopSurfaceVariant.Console)
        {
            Dock = DockStyle.Fill,
            CornerRadius = _tokens.Geometria.RaioMedio,
            Padding = new Padding(1)
        };
        var cabecalhoConsole = CriarCabecalhoConsole();
        var eventos = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _paleta.ConsoleFundo,
            Padding = new Padding(0, 0, 1, 0)
        };
        eventos.SizeChanged += (_, _) => AjustarLarguraFilhos(eventos, 1);
        console.Controls.Add(eventos);
        console.Controls.Add(cabecalhoConsole);

        void AtualizarConsole()
        {
            NivelEventoPoc? nivelSelecionado = nivel.SelectedIndex switch
            {
                1 => NivelEventoPoc.Info,
                2 => NivelEventoPoc.Aviso,
                3 => NivelEventoPoc.Erro,
                _ => null
            };
            var componenteSelecionado = componente.SelectedIndex <= 0 ? null : componente.Text;
            var agora = DateTimeOffset.Now;
            DateTimeOffset? inicioUtc = intervalo.SelectedIndex switch
            {
                0 => agora.AddHours(-24),
                1 => agora.AddHours(-1),
                2 => agora.AddDays(-7),
                3 => agora.AddDays(-14),
                _ => null
            };
            var visiveis = _observabilidade.Filtrar(
                busca.Text,
                nivelSelecionado,
                componenteSelecionado,
                inicioUtc);
            var valores = _observabilidade.CalcularMetricas(visiveis);

            totalEventos.Text = valores.TotalEventos.ToString(CultureInfo.InvariantCulture);
            totalAlertas.Text = valores.Alertas.ToString(CultureInfo.InvariantCulture);
            duracaoMedia.Text = valores.DuracaoMediaMs <= 0 ? "0 ms" : $"{valores.DuracaoMediaMs:0} ms";
            jobsNaFila.Text = valores.JobsNaFila.ToString(CultureInfo.InvariantCulture);

            eventos.SuspendLayout();
            eventos.Controls.Clear();
            foreach (var evento in visiveis)
            {
                eventos.Controls.Add(CriarLinhaConsole(evento));
            }

            if (visiveis.Count == 0)
            {
                eventos.Controls.Add(new Label
                {
                    Text = "Nenhum evento corresponde aos filtros.",
                    AutoSize = true,
                    ForeColor = _paleta.ConsoleSecundario,
                    Font = new Font(_tokens.Tipografia.Mono, _tokens.Tipografia.Dado, FontStyle.Regular, GraphicsUnit.Point),
                    Margin = new Padding(16, 22, 0, 0)
                });
            }

            AjustarLarguraFilhos(eventos, 1);
            eventos.ResumeLayout();
        }

        busca.TextChanged += (_, _) => AtualizarConsole();
        nivel.SelectedIndexChanged += (_, _) => AtualizarConsole();
        intervalo.SelectedIndexChanged += (_, _) => AtualizarConsole();
        componente.SelectedIndexChanged += (_, _) => AtualizarConsole();
        _atualizarConsoleObservabilidade = AtualizarConsole;

        corpo.Controls.Add(console);
        corpo.Controls.Add(filtros);
        corpo.Controls.Add(metricas);
        corpo.Controls.Add(faixa);
        AtualizarConsole();
        return pagina;
    }

    private DesktopSurfacePanel CriarCartaoMetricaObservabilidade(
        string titulo,
        string detalhe,
        Color corValor,
        out Label valor)
    {
        var cartao = CriarCartao(DesktopSurfaceVariant.Base, interactive: true, accent: corValor);
        cartao.Dock = DockStyle.Fill;
        cartao.Margin = new Padding(0, 0, 10, 0);
        cartao.Padding = new Padding(16, 10, 16, 10);
        valor = CriarLabel("0", 17F, corValor, new Point(16, 28), FontStyle.Bold);
        valor.Font = new Font(_tokens.Tipografia.Mono, 17F, FontStyle.Bold, GraphicsUnit.Point);
        cartao.Controls.Add(CriarLabel(detalhe, 8.5F, _paleta.TextoSecundario, new Point(16, 59)));
        cartao.Controls.Add(valor);
        var tituloLabel = CriarLabel(titulo, 8F, _paleta.TextoSecundario, new Point(16, 10), FontStyle.Bold);
        tituloLabel.Font = new Font(_tokens.Tipografia.Mono, 8F, FontStyle.Bold, GraphicsUnit.Point);
        cartao.Controls.Add(tituloLabel);
        return cartao;
    }

    private TableLayoutPanel CriarCabecalhoConsole()
    {
        var grade = CriarGradeConsole(34, _paleta.ConsoleLinha);
        grade.Dock = DockStyle.Top;
        grade.Controls.Add(CriarCelulaConsole("HORA", _paleta.ConsoleSecundario, FontStyle.Bold), 0, 0);
        grade.Controls.Add(CriarCelulaConsole("NÍVEL", _paleta.ConsoleSecundario, FontStyle.Bold), 1, 0);
        grade.Controls.Add(CriarCelulaConsole("COMPONENTE", _paleta.ConsoleSecundario, FontStyle.Bold), 2, 0);
        grade.Controls.Add(CriarCelulaConsole("EVENTO E MENSAGEM", _paleta.ConsoleSecundario, FontStyle.Bold), 3, 0);
        grade.Controls.Add(CriarCelulaConsole("CORRELAÇÃO", _paleta.ConsoleSecundario, FontStyle.Bold), 4, 0);
        grade.Controls.Add(CriarCelulaConsole("DURAÇÃO", _paleta.ConsoleSecundario, FontStyle.Bold), 5, 0);
        return grade;
    }

    private TableLayoutPanel CriarLinhaConsole(EventoObservabilidadePoc evento)
    {
        var grade = CriarGradeConsole(42, _paleta.ConsoleFundo);
        grade.Margin = new Padding(0);
        grade.Controls.Add(CriarCelulaConsole(evento.CriadoEm.ToString("HH:mm:ss", CultureInfo.InvariantCulture), _paleta.ConsoleSecundario), 0, 0);
        grade.Controls.Add(CriarCelulaConsole(FormatarNivel(evento.Nivel), CorNivel(evento.Nivel), FontStyle.Bold), 1, 0);
        grade.Controls.Add(CriarCelulaConsole(evento.Componente, _paleta.ConsoleTexto), 2, 0);
        grade.Controls.Add(CriarCelulaConsole($"{evento.Evento}  {evento.Mensagem}", _paleta.ConsoleTexto), 3, 0);
        grade.Controls.Add(CriarCelulaConsole(evento.CorrelacaoId, _paleta.ConsoleSecundario), 4, 0);
        grade.Controls.Add(CriarCelulaConsole(evento.DuracaoMs is null ? "" : $"{evento.DuracaoMs:0} ms", _paleta.ConsoleSecundario), 5, 0);
        grade.Paint += (_, e) =>
        {
            using var caneta = new Pen(_paleta.ConsoleLinha);
            e.Graphics.DrawLine(caneta, 0, grade.Height - 1, grade.Width, grade.Height - 1);
        };
        return grade;
    }

    private static TableLayoutPanel CriarGradeConsole(int altura, Color fundo)
    {
        var grade = new TableLayoutPanel
        {
            Height = altura,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = fundo,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88F));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124F));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
        grade.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        return grade;
    }

    private Label CriarCelulaConsole(string texto, Color cor, FontStyle estilo = FontStyle.Regular) => new()
    {
        Text = texto,
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = cor,
        BackColor = _paleta.ConsoleFundo,
        Font = new Font(_tokens.Tipografia.Mono, 8F, estilo, GraphicsUnit.Point),
        Padding = new Padding(8, 0, 4, 0)
    };

    private Color CorNivel(NivelEventoPoc nivel) => nivel switch
    {
        NivelEventoPoc.Aviso => _paleta.Destaque,
        NivelEventoPoc.Erro => _paleta.Perigo,
        _ => _paleta.Positivo
    };

    private static string FormatarNivel(NivelEventoPoc nivel) => nivel switch
    {
        NivelEventoPoc.Aviso => "WARN",
        NivelEventoPoc.Erro => "ERROR",
        _ => "INFO"
    };

    private Panel CriarTelaConfiguracoes()
    {
        var ambienteAtual = _sessao.Ambiente;
        var editarAvancado = !_sessao.ModoDemonstracao && ambienteAtual is not null
            ? CriarBotaoSecundario("Editar configuração avançada", (_, _) =>
            {
                var inicio = new ProcessStartInfo("notepad.exe") { UseShellExecute = true };
                inicio.ArgumentList.Add(ambienteAtual.CaminhoConfiguracao);
                Process.Start(inicio);
            })
            : null;
        var pagina = CriarPagina(
            "Configurações",
            "Tudo permanece local nesta máquina",
            editarAvancado,
            out var corpo);
        if (!_sessao.ModoDemonstracao)
        {
            var ambiente = ambienteAtual;
            var conteudo = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = _paleta.Superficies.Canvas,
                Padding = new Padding(0, 0, 8, 0)
            };
            conteudo.Controls.Add(CriarFormularioConfiguracaoReal(ambiente));
            conteudo.Controls.Add(CriarBlocoTexto(
                "Configuração local em uso",
                ambiente is null
                    ? ["A configuração real está ativa, mas seus caminhos não foram fornecidos à janela."]
                    :
                    [
                        $"Arquivo: {ambiente.CaminhoConfiguracao}",
                        $"Banco SQLite: {ambiente.CaminhoBanco}",
                        $"Arquivo de reuniões: {ambiente.DiretorioArquivo}",
                        $"CLI de atas: {ambiente.NomeCli}"
                    ]));
            var prontidao = ambiente?.Prontidao ?? [];
            conteudo.Controls.Add(CriarBlocoTexto(
                "Prontidão do pipeline",
                prontidao.Count == 0
                    ? ["Execute Diagnósticos pela bandeja para verificar os componentes locais."]
                    : prontidao.Select(item =>
                        $"{(item.Disponivel ? "PRONTO" : "PENDENTE")}  {item.Nome}: {item.Mensagem}")
                        .ToArray()));
            if (prontidao.Any(item => !item.Disponivel))
            {
                conteudo.Controls.Add(CriarAlerta(
                    "O aplicativo continuará aberto. Configure apenas os componentes marcados como pendentes."));
            }

            conteudo.SizeChanged += (_, _) => AjustarLarguraFilhos(conteudo);
            corpo.Controls.Add(conteudo);
            AjustarLarguraFilhos(conteudo);

            return pagina;
        }

        var grade = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = _paleta.Superficies.Canvas,
            Padding = new Padding(0)
        };
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grade.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var esquerda = CriarCartao(DesktopSurfaceVariant.Base);
        esquerda.Dock = DockStyle.Fill;
        esquerda.Margin = new Padding(0, 0, 8, 0);
        esquerda.Padding = new Padding(22);
        var fluxoEsquerdo = CriarColunaConfiguracao();
        fluxoEsquerdo.Controls.Add(CriarTituloConfiguracao("Comportamento"));
        fluxoEsquerdo.Controls.Add(CriarOpcaoConfiguracao("Iniciar com o Windows", "Mantém o app pronto para detectar reuniões.", true));
        fluxoEsquerdo.Controls.Add(CriarOpcaoConfiguracao("Gravação automática", "Usa somente plataformas permitidas.", false));
        fluxoEsquerdo.Controls.Add(CriarTituloConfiguracao("Inteligência artificial"));
        fluxoEsquerdo.Controls.Add(CriarCampoConfiguracao("CLI para geração de atas", ["Codex CLI", "Claude CLI", "Ollama"]));
        esquerda.Controls.Add(fluxoEsquerdo);

        var direita = CriarCartao(DesktopSurfaceVariant.Base);
        direita.Dock = DockStyle.Fill;
        direita.Margin = new Padding(8, 0, 0, 0);
        direita.Padding = new Padding(22);
        var fluxoDireito = CriarColunaConfiguracao();
        fluxoDireito.Controls.Add(CriarTituloConfiguracao("Armazenamento"));
        fluxoDireito.Controls.Add(CriarCampoConfiguracaoTexto("Pasta de arquivamento", "%LOCALAPPDATA%\\Anamnesis\\Arquivo"));
        fluxoDireito.Controls.Add(CriarCampoConfiguracao("Retenção das gravações", ["7 dias", "15 dias", "30 dias", "Não remover"]));
        fluxoDireito.Controls.Add(CriarTituloConfiguracao("Diagnósticos"));
        fluxoDireito.Controls.Add(CriarLinhaDiagnostico("OBS WebSocket", "Pronto"));
        fluxoDireito.Controls.Add(CriarLinhaDiagnostico("Docker e Whisper", "Prontos"));
        fluxoDireito.Controls.Add(CriarLinhaDiagnostico("Codex CLI", "Autenticado"));
        direita.Controls.Add(fluxoDireito);

        grade.Controls.Add(esquerda, 0, 0);
        grade.Controls.Add(direita, 1, 0);
        corpo.Controls.Add(grade);
        AjustarLarguraFilhos(fluxoEsquerdo, 0);
        AjustarLarguraFilhos(fluxoDireito, 0);
        return pagina;
    }

    private FlowLayoutPanel CriarColunaConfiguracao()
    {
        var fluxo = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = _paleta.Superficies.Painel
        };
        fluxo.SizeChanged += (_, _) => AjustarLarguraFilhos(fluxo, 0);
        return fluxo;
    }

    private DesktopShadowPanel CriarFormularioConfiguracaoReal(DesktopRuntimeInfo? ambiente)
    {
        var configuracao = _configuracaoAtual ?? ambiente?.Configuracao ??
            ConfiguracaoAnamnesis.CriarPadrao() with
            {
                CaminhoBanco = ambiente?.CaminhoBanco ?? ConfiguracaoAnamnesis.CriarPadrao().CaminhoBanco,
                DiretorioArquivo = ambiente?.DiretorioArquivo ?? ConfiguracaoAnamnesis.CriarPadrao().DiretorioArquivo,
                NomeCli = ambiente?.NomeCli ?? "CLI configurada"
            };
        var iniciarComWindows = _obterInicializacaoWindows?.Invoke() ?? false;
        var cartao = new DesktopShadowPanel(
            _paleta,
            _tokens,
            _politicaVisual,
            DesktopSurfaceVariant.Elevated)
        {
            Height = 510,
            Padding = new Padding(24),
            CornerRadius = _tokens.Geometria.RaioMedio,
            AccessibleName = "Formulário de configurações do Anamnesis"
        };
        var titulo = CriarLabel(
            "Ajustes do produto",
            15F,
            _paleta.Texto,
            new Point(24, 22),
            FontStyle.Bold);
        var descricao = CriarLabel(
            "Altere somente o necessário. Senhas e opções avançadas permanecem preservadas.",
            9.5F,
            _paleta.TextoSecundario,
            new Point(24, 54));
        cartao.Controls.Add(titulo);
        cartao.Controls.Add(descricao);

        var grade = new TableLayoutPanel
        {
            Location = new Point(24, 90),
            Height = 330,
            ColumnCount = 2,
            RowCount = 1,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = _paleta.Superficies.Painel,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grade.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        cartao.Resize += (_, _) => grade.Width = Math.Max(320, cartao.ClientSize.Width - 48);

        var esquerda = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _paleta.Superficies.Painel,
            Padding = new Padding(0, 0, 16, 0)
        };
        var direita = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _paleta.Superficies.Painel,
            Padding = new Padding(16, 0, 0, 0)
        };
        esquerda.SizeChanged += (_, _) => AjustarLarguraFilhos(esquerda, 0);
        direita.SizeChanged += (_, _) => AjustarLarguraFilhos(direita, 0);

        var inicializacao = new DesktopToggle(_paleta, _tokens)
        {
            Checked = iniciarComWindows,
            AccessibleName = "Iniciar Anamnesis com o Windows"
        };
        esquerda.Controls.Add(CriarCampoToggleConfiguracao(
            "Iniciar com o Windows",
            "Mantém o Anamnesis pronto na bandeja.",
            inicializacao));
        var enderecoObs = new DesktopTextField(_paleta, _tokens, "ws://127.0.0.1:4455")
        {
            Text = configuracao.EnderecoObs,
            AccessibleName = "Endereço local do OBS"
        };
        esquerda.Controls.Add(CriarCampoFormularioConfiguracao(
            "Endereço local do OBS",
            "WebSocket local usado para iniciar e encerrar a captura.",
            enderecoObs));
        var diretorio = new DesktopTextField(_paleta, _tokens, "Escolha uma pasta local")
        {
            Text = configuracao.DiretorioArquivo,
            AccessibleName = "Pasta das reuniões"
        };
        esquerda.Controls.Add(CriarCampoFormularioConfiguracao(
            "Pasta das reuniões",
            "Destino local das atas, transcrições e artefatos.",
            diretorio));

        var nomeCli = new DesktopTextField(_paleta, _tokens, "Exemplo: Codex CLI")
        {
            Text = configuracao.NomeCli,
            AccessibleName = "Nome da CLI de atas"
        };
        direita.Controls.Add(CriarCampoFormularioConfiguracao(
            "CLI para gerar atas",
            "Nome visível da ferramenta autenticada.",
            nomeCli));
        var executavelCli = new DesktopTextField(_paleta, _tokens, "Caminho do executável")
        {
            Text = configuracao.CaminhoExecutavelCli,
            AccessibleName = "Executável da CLI de atas"
        };
        direita.Controls.Add(CriarCampoFormularioConfiguracao(
            "Executável da CLI",
            "Deixe o caminho descoberto automaticamente ou ajuste aqui.",
            executavelCli));
        direita.Controls.Add(CriarLinhaDiagnostico(
            "Aplicação das mudanças",
            "Reinício"));

        grade.Controls.Add(esquerda, 0, 0);
        grade.Controls.Add(direita, 1, 0);
        cartao.Controls.Add(grade);

        var retorno = CriarLabel(
            _salvarConfiguracao is null
                ? "Prévia visual: o salvamento real está disponível no aplicativo instalado."
                : "Nenhuma alteração pendente.",
            9F,
            _paleta.TextoSecundario,
            new Point(24, 456));
        retorno.AccessibleRole = AccessibleRole.StatusBar;
        retorno.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        cartao.Controls.Add(retorno);
        DesktopActionButton? salvar = null;
        salvar = CriarBotaoPrimario(
            "Salvar alterações",
            async (_, _) =>
            {
                try
                {
                    salvar!.Enabled = false;
                    retorno.Text = "Validando e salvando localmente...";
                    retorno.ForeColor = _paleta.TextoSecundario;
                    var rascunho = new DesktopConfigurationDraft(
                        diretorio.Text,
                        enderecoObs.Text,
                        nomeCli.Text,
                        executavelCli.Text,
                        inicializacao.Checked);
                    var novaConfiguracao = rascunho.Build(configuracao);
                    if (_salvarConfiguracao is not null)
                    {
                        await _salvarConfiguracao(novaConfiguracao, _lifetime.Token);
                        _definirInicializacaoWindows?.Invoke(inicializacao.Checked);
                        _configuracaoAtual = novaConfiguracao;
                        configuracao = novaConfiguracao;
                        retorno.Text = "Configuração salva. As mudanças entram em vigor na próxima abertura.";
                        retorno.ForeColor = _paleta.Positivo;
                    }
                    else
                    {
                        retorno.Text = "Prévia validada. Nenhum arquivo foi alterado no modo de demonstração.";
                        retorno.ForeColor = _paleta.Positivo;
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    retorno.Text = exception.Message;
                    retorno.ForeColor = _paleta.Perigo;
                }
                finally
                {
                    salvar!.Enabled = true;
                }
            },
            DesktopActionIcon.Save);
        salvar.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        salvar.Location = new Point(cartao.ClientSize.Width - salvar.Width - 24, 440);
        cartao.Resize += (_, _) => salvar.Left = cartao.ClientSize.Width - salvar.Width - 24;
        cartao.Controls.Add(salvar);
        return cartao;
    }

    private Panel CriarCampoFormularioConfiguracao(
        string titulo,
        string descricao,
        Control campo)
    {
        var grupo = new Panel
        {
            Height = 104,
            BackColor = _paleta.Superficies.Painel,
            Margin = Padding.Empty
        };
        campo.Location = new Point(0, 54);
        campo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grupo.Resize += (_, _) => campo.Width = grupo.ClientSize.Width;
        grupo.Controls.Add(campo);
        grupo.Controls.Add(CriarLabel(descricao, 8.8F, _paleta.TextoSecundario, new Point(0, 27)));
        grupo.Controls.Add(CriarLabel(titulo, 10F, _paleta.Texto, new Point(0, 2), FontStyle.Bold));
        return grupo;
    }

    private Panel CriarCampoToggleConfiguracao(
        string titulo,
        string descricao,
        DesktopToggle toggle)
    {
        var grupo = new Panel
        {
            Height = 104,
            BackColor = _paleta.Superficies.Painel,
            Margin = Padding.Empty
        };
        toggle.Location = new Point(0, 54);
        grupo.Controls.Add(toggle);
        grupo.Controls.Add(CriarLabel(descricao, 8.8F, _paleta.TextoSecundario, new Point(0, 27)));
        grupo.Controls.Add(CriarLabel(titulo, 10F, _paleta.Texto, new Point(0, 2), FontStyle.Bold));
        return grupo;
    }

    private Panel CriarPagina(string titulo, string subtitulo, Control? acao, out Panel corpo)
    {
        var pagina = new Panel { Dock = DockStyle.Fill, BackColor = _paleta.Superficies.Canvas };
        var cabecalho = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = _paleta.Superficies.Canvas };
        var tituloLabel = new Label
        {
            Text = titulo,
            AutoSize = true,
            ForeColor = _paleta.Texto,
            Font = new Font(_tokens.Tipografia.Display, _tokens.Tipografia.Titulo, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(0, 0)
        };
        var subtituloLabel = new Label
        {
            Text = subtitulo,
            AutoSize = true,
            ForeColor = _paleta.TextoSecundario,
            Location = new Point(2, 38)
        };
        cabecalho.Controls.Add(tituloLabel);
        cabecalho.Controls.Add(subtituloLabel);
        if (acao is not null)
        {
            acao.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            acao.Location = new Point(cabecalho.Width - acao.Width, 6);
            cabecalho.Controls.Add(acao);
            cabecalho.Resize += (_, _) => acao.Left = cabecalho.ClientSize.Width - acao.Width;
        }

        corpo = new Panel { Dock = DockStyle.Fill, BackColor = _paleta.Superficies.Canvas };
        pagina.Controls.Add(corpo);
        pagina.Controls.Add(cabecalho);
        return pagina;
    }

    private DesktopShadowPanel CriarCartaoStatus(string titulo, string estado, string detalhe)
    {
        var cartao = new DesktopShadowPanel(_paleta, _tokens, _politicaVisual, DesktopSurfaceVariant.Base)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(16),
            Interactive = true,
            CornerRadius = _tokens.Geometria.RaioMedio
        };
        cartao.Controls.Add(CriarLabel(detalhe, 8.5F, _paleta.TextoSecundario, new Point(16, 62)));
        cartao.Controls.Add(new DesktopStatusBadge(_paleta, _tokens, estado)
        {
            Location = new Point(16, 32)
        });
        cartao.Controls.Add(CriarLabel(titulo, 9F, _paleta.Texto, new Point(16, 12), FontStyle.Bold));
        return cartao;
    }

    private DesktopSurfacePanel CriarCartao(
        DesktopSurfaceVariant variant = DesktopSurfaceVariant.Base,
        bool interactive = false,
        Color? accent = null)
    {
        return new DesktopSurfacePanel(_paleta, _tokens, _politicaVisual, variant)
        {
            Interactive = interactive,
            AccentColor = accent,
            CornerRadius = _tokens.Geometria.RaioMedio
        };
    }

    private Panel CriarCabecalhoSecao(string titulo, string acao, EventHandler clique)
    {
        var painel = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = _paleta.Superficies.Canvas };
        var label = CriarLabel(titulo, 13F, _paleta.Texto, new Point(0, 16), FontStyle.Bold);
        var botao = CriarBotaoSecundario(acao, clique);
        botao.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        botao.Location = new Point(painel.Width - botao.Width, 7);
        painel.Resize += (_, _) => botao.Left = painel.ClientSize.Width - botao.Width;
        painel.Controls.Add(botao);
        painel.Controls.Add(label);
        return painel;
    }

    private FlowLayoutPanel CriarListaReunioes(IEnumerable<ReuniaoDesktopPoc> reunioes)
    {
        var itens = reunioes.ToArray();
        var lista = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = _paleta.Superficies.Canvas,
            Padding = new Padding(0, 0, 8, 0),
            AccessibleRole = AccessibleRole.List,
            AccessibleName = "Histórico de reuniões"
        };

        foreach (var reuniao in itens)
        {
            lista.Controls.Add(CriarLinhaReuniao(reuniao));
        }

        if (itens.Length == 0)
        {
            var vazio = CriarCartao(DesktopSurfaceVariant.Base);
            vazio.Height = 112;
            vazio.Margin = new Padding(0, 0, 0, 9);
            vazio.Controls.Add(CriarLabel(
                "A primeira reunião aparecerá aqui após você iniciar e encerrar uma gravação.",
                9.5F,
                _paleta.TextoSecundario,
                new Point(20, 58)));
            vazio.Controls.Add(CriarLabel(
                "Nenhuma reunião registrada",
                12F,
                _paleta.Texto,
                new Point(20, 24),
                FontStyle.Bold));
            lista.Controls.Add(vazio);
        }

        lista.SizeChanged += (_, _) => AjustarLarguraFilhos(lista);
        AjustarLarguraFilhos(lista);
        return lista;
    }

    private DesktopReuniaoListItem CriarLinhaReuniao(ReuniaoDesktopPoc reuniao)
    {
        var item = new DesktopReuniaoListItem(_paleta, _tokens, _politicaVisual, reuniao);
        item.Click += async (_, _) => await AbrirDetalheAsync(
            reuniao.Id,
            reuniao.SecaoCorrespondente ?? "Resumo");
        return item;
    }
    private FlowLayoutPanel CriarListaInformativa(IEnumerable<(string Titulo, string Detalhe, string Estado)> itens)
    {
        var lista = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = _paleta.Superficies.Canvas,
            Padding = new Padding(0, 0, 8, 0)
        };
        foreach (var item in itens)
        {
            var linha = CriarCartao(DesktopSurfaceVariant.Base, interactive: true, accent: _paleta.Positivo);
            linha.Height = 78;
            linha.Margin = new Padding(0, 0, 0, 9);
            linha.Controls.Add(CriarLabel(item.Estado, 10F, _paleta.Positivo, new Point(0, 27), FontStyle.Bold));
            var estado = linha.Controls[^1];
            estado.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            linha.Resize += (_, _) => estado.Left = linha.ClientSize.Width - estado.Width - 22;
            linha.Controls.Add(CriarLabel(item.Detalhe, 9F, _paleta.TextoSecundario, new Point(20, 44)));
            linha.Controls.Add(CriarLabel(item.Titulo, 11F, _paleta.Texto, new Point(20, 17), FontStyle.Bold));
            lista.Controls.Add(linha);
        }

        lista.SizeChanged += (_, _) => AjustarLarguraFilhos(lista);
        AjustarLarguraFilhos(lista);
        return lista;
    }

    private DesktopShadowPanel CriarAviso(string mensagem)
    {
        var aviso = new DesktopShadowPanel(_paleta, _tokens, _politicaVisual, DesktopSurfaceVariant.Base)
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 12),
            CornerRadius = _tokens.Geometria.RaioMedio,
            AccentColor = _paleta.Positivo
        };
        aviso.Controls.Add(new Label
        {
            Text = $"Pronto: {mensagem}",
            AutoSize = true,
            ForeColor = _paleta.Positivo,
            BackColor = _paleta.Superficies.Painel,
            Dock = DockStyle.Left
        });
        return aviso;
    }

    private DesktopShadowPanel CriarAlerta(string mensagem)
    {
        var alerta = new DesktopShadowPanel(_paleta, _tokens, _politicaVisual, DesktopSurfaceVariant.Base)
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 12),
            CornerRadius = _tokens.Geometria.RaioMedio,
            AccentColor = _paleta.Destaque
        };
        alerta.Controls.Add(new Label
        {
            Text = $"Atenção: {mensagem}",
            AutoSize = true,
            ForeColor = _paleta.Destaque,
            BackColor = _paleta.Superficies.Painel,
            Dock = DockStyle.Left
        });
        return alerta;
    }

    private DesktopShadowPanel CriarBlocoTexto(string titulo, IEnumerable<string> linhas)
    {
        var itens = linhas.ToArray();
        var conteudo = itens.Length == 0
            ? "Nenhum conteúdo disponível."
            : string.Join(Environment.NewLine, itens);
        var linhasVisiveis = itens.Sum(item => Math.Max(1, (item.Length + 109) / 110));
        var bloco = new DesktopShadowPanel(_paleta, _tokens, _politicaVisual, DesktopSurfaceVariant.Base)
        {
            Height = Math.Clamp(106 + linhasVisiveis * 22, 156, 460),
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(24, 20, 24, 20),
            CornerRadius = _tokens.Geometria.RaioMedio
        };

        var grade = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = _paleta.Superficies.Painel,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        grade.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        grade.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var cabecalho = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _paleta.Superficies.Painel,
            Margin = Padding.Empty
        };
        var tituloLabel = new Label
        {
            Text = titulo,
            Dock = DockStyle.Fill,
            ForeColor = _paleta.Texto,
            Font = new Font(_tokens.Tipografia.Display, 11.5F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var copiar = CriarBotaoSecundario("Copiar texto", (_, _) =>
        {
            Clipboard.SetText(conteudo);
        });
        copiar.Dock = DockStyle.Right;
        copiar.MinimumSize = new Size(104, 32);
        copiar.Height = 32;
        copiar.Padding = new Padding(10, 0, 10, 0);

        var texto = new RichTextBox
        {
            Text = conteudo,
            Dock = DockStyle.Fill,
            ReadOnly = true,
            ShortcutsEnabled = true,
            HideSelection = false,
            DetectUrls = false,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = _paleta.Superficies.Painel,
            ForeColor = _paleta.Texto,
            Font = new Font(_tokens.Tipografia.Interface, 10.5F, FontStyle.Regular, GraphicsUnit.Point),
            TabStop = true,
            AccessibleName = $"{titulo}: texto selecionável"
        };

        cabecalho.Controls.Add(tituloLabel);
        cabecalho.Controls.Add(copiar);
        grade.Controls.Add(cabecalho, 0, 0);
        grade.Controls.Add(texto, 0, 1);

        bloco.Controls.Add(grade);
        return bloco;
    }

    private DesktopShadowPanel CriarBlocoLembretes(Guid reuniaoId, IReadOnlyList<string> tarefas)
    {
        var bloco = new DesktopShadowPanel(_paleta, _tokens, _politicaVisual, DesktopSurfaceVariant.Base)
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            MinimumSize = new Size(0, 92),
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(22, 18, 22, 18),
            CornerRadius = _tokens.Geometria.RaioMedio
        };

        var lista = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        foreach (var tarefa in tarefas)
        {
            var linha = new Panel
            {
                Width = 820,
                Height = 54,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 8)
            };
            var descricao = CriarLabel(
                tarefa,
                9.5F,
                _paleta.Texto,
                new Point(0, 8));
            descricao.AutoEllipsis = true;
            descricao.Size = new Size(610, 36);
            var criar = CriarBotaoSecundario("Criar lembrete", async (_, _) =>
                await SolicitarLembreteAsync(reuniaoId, tarefa));
            criar.Location = new Point(630, 5);
            criar.Size = new Size(170, 40);
            linha.Controls.Add(criar);
            linha.Controls.Add(descricao);
            lista.Controls.Add(linha);
        }

        bloco.Controls.Add(lista);
        bloco.Controls.Add(CriarLabel(
            "LEMBRETES LOCAIS",
            8.5F,
            _paleta.Destaque,
            new Point(22, 18),
            FontStyle.Bold));
        lista.Padding = new Padding(0, 30, 0, 0);
        return bloco;
    }

    private async Task SolicitarLembreteAsync(Guid reuniaoId, string tarefa)
    {
        using var dialogo = new LembreteTarefaForm(
            tarefa,
            _tema);
        if (dialogo.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await CriarLembreteConfirmadoAgoraAsync(reuniaoId, tarefa, dialogo.Horario);
            MessageBox.Show(
                $"Lembrete salvo para {dialogo.Horario:dd/MM/yyyy 'às' HH:mm}.",
                "Lembrete criado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Não foi possível criar o lembrete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    internal Task CriarLembreteConfirmadoAgoraAsync(
        Guid reuniaoId,
        string tarefa,
        DateTimeOffset horario) =>
        _criarLembrete is null
            ? throw new InvalidOperationException("A criação de lembretes não está configurada.")
            : _criarLembrete(reuniaoId, tarefa, horario, _lifetime.Token);

    private Label CriarTituloConfiguracao(string titulo) => new()
    {
        Text = titulo,
        AutoSize = false,
        Height = 38,
        Font = new Font(_tokens.Tipografia.Display, 11F, FontStyle.Bold, GraphicsUnit.Point),
        ForeColor = _paleta.Destaque,
        Padding = new Padding(0, 10, 0, 0),
        Margin = new Padding(0, 8, 0, 4)
    };

    private Panel CriarOpcaoConfiguracao(string titulo, string descricao, bool ativo)
    {
        var linha = CriarLinhaConfiguracaoBase(68);
        var alternar = new DesktopToggle(_paleta, _tokens)
        {
            Checked = ativo,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(linha.Width - 112, 17),
            Cursor = Cursors.Hand
        };
        linha.Resize += (_, _) => alternar.Left = linha.ClientSize.Width - 112;
        linha.Controls.Add(alternar);
        linha.Controls.Add(CriarLabel(descricao, 9F, _paleta.TextoSecundario, new Point(0, 37)));
        linha.Controls.Add(CriarLabel(titulo, 10F, _paleta.Texto, new Point(0, 10), FontStyle.Bold));
        return linha;
    }

    private Panel CriarCampoConfiguracao(string titulo, string[] opcoes)
    {
        var linha = CriarLinhaConfiguracaoBase(74);
        var combo = CriarCombo(opcoes);
        combo.Width = 260;
        combo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        combo.Location = new Point(linha.Width - combo.Width, 28);
        linha.Resize += (_, _) => combo.Left = linha.ClientSize.Width - combo.Width;
        linha.Controls.Add(combo);
        linha.Controls.Add(CriarLabel(titulo, 10F, _paleta.Texto, new Point(0, 10), FontStyle.Bold));
        return linha;
    }

    private Panel CriarCampoConfiguracaoTexto(string titulo, string valor)
    {
        var linha = CriarLinhaConfiguracaoBase(74);
        var campo = CriarCampoTexto("Informe uma pasta local");
        campo.Text = valor;
        campo.Width = 360;
        campo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        campo.Location = new Point(linha.Width - campo.Width, 28);
        linha.Resize += (_, _) => campo.Left = linha.ClientSize.Width - campo.Width;
        linha.Controls.Add(campo);
        linha.Controls.Add(CriarLabel(titulo, 10F, _paleta.Texto, new Point(0, 10), FontStyle.Bold));
        return linha;
    }

    private Panel CriarLinhaDiagnostico(string nome, string estado)
    {
        var linha = CriarLinhaConfiguracaoBase(48);
        var valor = CriarLabel(estado, 9.5F, _paleta.Positivo, new Point(0, 15), FontStyle.Bold);
        valor.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        linha.Resize += (_, _) => valor.Left = linha.ClientSize.Width - valor.Width;
        linha.Controls.Add(valor);
        linha.Controls.Add(CriarLabel(nome, 10F, _paleta.Texto, new Point(0, 15)));
        return linha;
    }

    private Label CriarLabel(string texto, float tamanho, Color cor, Point local, FontStyle estilo = FontStyle.Regular) =>
        new()
        {
            Text = texto,
            AutoSize = true,
            ForeColor = cor,
            Font = new Font(_tokens.Tipografia.Interface, tamanho, estilo, GraphicsUnit.Point),
            Location = local
        };

    private Panel CriarLinhaConfiguracaoBase(int altura)
    {
        var linha = new Panel
        {
            Height = altura,
            BackColor = _paleta.Superficies.Painel
        };
        linha.Paint += (_, e) =>
        {
            using var borda = new Pen(_paleta.Borda, 1F);
            e.Graphics.DrawLine(borda, 0, linha.Height - 1, linha.Width, linha.Height - 1);
        };
        return linha;
    }

    private DesktopTextField CriarCampoTexto(string placeholder) =>
        new(_paleta, _tokens, placeholder);

    private DesktopSelectField CriarCombo(string[] opcoes)
        => new(_paleta, _tokens, opcoes);

    private DesktopActionButton CriarBotaoPrimario(
        string texto,
        EventHandler clique,
        DesktopActionIcon icon = DesktopActionIcon.None) =>
        CriarBotao(texto, DesktopActionVariant.Primary, clique, icon);

    private DesktopActionButton CriarBotaoPerigo(
        string texto,
        EventHandler clique,
        DesktopActionIcon icon = DesktopActionIcon.None) =>
        CriarBotao(texto, DesktopActionVariant.Danger, clique, icon);

    private DesktopActionButton CriarBotaoSecundario(
        string texto,
        EventHandler clique,
        DesktopActionIcon icon = DesktopActionIcon.None) =>
        CriarBotao(texto, DesktopActionVariant.Secondary, clique, icon);

    private DesktopActionButton CriarBotao(
        string texto,
        DesktopActionVariant variant,
        EventHandler clique,
        DesktopActionIcon icon = DesktopActionIcon.None)
    {
        var botao = new DesktopActionButton(_paleta, _tokens, _politicaVisual, variant)
        {
            Text = texto,
            Icon = icon,
            AutoSize = true,
            MinimumSize = new Size(132, 40),
            Height = 40,
            Padding = new Padding(12, 0, 12, 0),
            Font = new Font(_tokens.Tipografia.Interface, 9.5F, FontStyle.Bold, GraphicsUnit.Point)
        };
        botao.Click += clique;
        return botao;
    }

    private async void IniciarGravacao()
    {
        var titulo = DialogoTituloReuniao.Solicitar(this, _tema, _politicaVisual);
        await IniciarGravacaoSeTituloConfirmadoAgoraAsync(titulo);
    }

    internal Task IniciarGravacaoAgoraAsync() =>
        IniciarGravacaoComTituloAgoraAsync(TituloReuniaoManual.Padrao);

    internal Task IniciarGravacaoSeTituloConfirmadoAgoraAsync(string? titulo) =>
        titulo is null ? Task.CompletedTask : IniciarGravacaoComTituloAgoraAsync(titulo);

    internal async Task IniciarGravacaoComTituloAgoraAsync(string titulo)
    {
        var cancellationToken = _lifetime.Token;
        try
        {
            await _sessao.IniciarGravacaoAsync(
                TituloReuniaoManual.Normalizar(titulo),
                cancellationToken);
            if (IsDisposed)
            {
                return;
            }

            _reuniaoAcompanhadaId = _sessao.ReuniaoAcompanhadaId ?? _sessao.ReuniaoAtivaId;
            if (_sessao.ModoDemonstracao)
            {
                _observabilidade.RegistrarInicioGravacao();
            }

            _timerProcessamento.Stop();
            _timerGravacao.Start();
            DefinirEstadoGlobal("Gravando", _paleta.Perigo, FundoEstadoAtencao());
            Navegar("ao-vivo");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsDisposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            MostrarFalhaSegura(
                $"Não foi possível iniciar a gravação. {exception.Message}",
                "Gravação não iniciada",
                exception,
                "iniciar_gravacao_desktop");
        }
    }

    private void AtualizarGravacao()
    {
        _sessao.AvancarGravacao();
        _onda++;
        if (_cronometro is not null)
        {
            _cronometro.Text = FormatarCronometro();
        }

        if (_audioSistema is not null)
        {
            _audioSistema.Value = 25 + ((_onda * 17) % 68);
        }

        if (_microfone is not null)
        {
            _microfone.Value = 18 + ((_onda * 23) % 62);
        }
    }

    private async void EncerrarGravacao() => await EncerrarGravacaoAgoraAsync();

    internal async Task EncerrarGravacaoAgoraAsync()
    {
        try
        {
            await _sessao.EncerrarGravacaoAsync(CancellationToken.None);
            if (IsDisposed)
            {
                return;
            }

            _timerGravacao.Stop();
            if (_sessao.ModoDemonstracao)
            {
                _observabilidade.RegistrarFimGravacao(_sessao.DuracaoGravacao);
            }

            AtualizarReuniaoAcompanhada();
            DefinirEstadoGlobal(
                "Processando localmente",
                _paleta.Destaque,
                _paleta.Superficies.DestaqueSuave);
            Navegar("atividade");
            if (_sessao.ModoDemonstracao)
            {
                _timerProcessamento.Start();
            }
        }
        catch (WorkerNaoIniciadoException exception)
        {
            if (IsDisposed)
            {
                return;
            }

            _timerGravacao.Stop();
            DefinirEstadoGlobal(
                "Processamento pendente",
                _paleta.Destaque,
                _paleta.Superficies.DestaqueSuave);
            AtualizarReuniaoAcompanhada();
            Navegar("atividade");
            MostrarFalhaSegura(
                $"{exception.Message} O job permanece salvo e pode ser retomado pelo Tray.",
                "Processamento pendente",
                exception,
                "iniciar_worker_desktop");
        }
        catch (Exception exception)
        {
            if (IsDisposed)
            {
                return;
            }

            MostrarFalhaSegura(
                $"Não foi possível encerrar a gravação. {exception.Message}",
                "Gravação não encerrada",
                exception,
                "finalizar_gravacao_desktop");
        }
    }

    private void ConcluirProcessamento()
    {
        _timerProcessamento.Stop();
        _sessao.ConcluirProcessamentoSimulado();
        if (_sessao.ModoDemonstracao)
        {
            _observabilidade.RegistrarConclusaoProcessamento();
        }
        DefinirEstadoGlobal("Tudo funcionando", _paleta.Positivo, _paleta.FundoPositivo);
        AtualizarReuniaoAcompanhada();
        AtualizarAcompanhamentoProcessamento();
        _atualizarConsoleObservabilidade?.Invoke();
    }

    private string FormatarCronometro() =>
        $"{(int)_sessao.DuracaoGravacao.TotalMinutes:00}:{_sessao.DuracaoGravacao.Seconds:00}";

    private async Task AbrirDetalheAsync(Guid reuniaoId, string abaInicial = "Resumo")
    {
        var versaoSolicitacao = ++_versaoNavegacao;
        try
        {
            var reuniao = await _sessao.ObterDetalheAsync(reuniaoId, _lifetime.Token);
            if (IsDisposed || versaoSolicitacao != _versaoNavegacao)
            {
                return;
            }

            if (reuniao is null)
            {
                MostrarFalhaSegura(
                    "A reunião não está mais disponível no banco local.",
                    "Reunião indisponível");
                return;
            }

            _reuniaoDetalheId = reuniaoId;
            _paginaAtual = "detalhe";
            _filtroObservabilidade = null;
            LimparReferenciasDaPagina();
            _conteudo.Controls.Clear();
            _conteudo.Controls.Add(CriarTelaDetalhe(reuniao, abaInicial));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            MostrarFalhaSegura(
                $"Não foi possível carregar a reunião. {exception.Message}",
                "Falha ao carregar",
                exception,
                "carregar_reuniao");
        }
    }

    private async Task AtualizarDadosReaisAsync()
    {
        if (_sessao.ModoDemonstracao || _pollingEmAndamento || IsDisposed)
        {
            return;
        }

        _pollingEmAndamento = true;
        try
        {
            await _sessao.AtualizarAsync(_lifetime.Token);
            if (IsDisposed)
            {
                return;
            }

            _observabilidade.SubstituirEventos(
                _sessao.EventosOperacionais,
                _sessao.JobsNaFila);
            AtualizarReuniaoAcompanhada();
            AtualizarEstadoGlobal();
            AtualizarAcompanhamentoProcessamento();
            _atualizarConsoleObservabilidade?.Invoke();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            DefinirEstadoGlobal(
                "Ação necessária",
                _paleta.Perigo,
                FundoEstadoAtencao(),
                exception.Message);
            await _sessao.RegistrarFalhaOperacionalAsync(
                "atualizar_desktop",
                exception,
                CancellationToken.None);
        }
        finally
        {
            _pollingEmAndamento = false;
        }
    }

    private void AtualizarEstadoGlobal()
    {
        if (_sessao.RecuperacaoPendente)
        {
            DefinirEstadoGlobal(
                "Recuperação pendente",
                _paleta.Destaque,
                _paleta.Superficies.DestaqueSuave,
                "Uma gravação anterior exige que você escolha encerrar ou manter.");
            return;
        }

        if (_sessao.Reunioes.Any(reuniao => string.Equals(reuniao.Status, "Falha", StringComparison.Ordinal)))
        {
            DefinirEstadoGlobal("Ação necessária", _paleta.Perigo, FundoEstadoAtencao());
            return;
        }

        var (texto, corTexto, corFundo) = _sessao.Etapa switch
        {
            EtapaDesktopPoc.Gravando => ("Gravando", _paleta.Perigo, FundoEstadoAtencao()),
            EtapaDesktopPoc.Processando => (
                "Processando localmente",
                _paleta.Destaque,
                _paleta.Superficies.DestaqueSuave),
            _ => ("Tudo funcionando", _paleta.Positivo, _paleta.FundoPositivo)
        };
        DefinirEstadoGlobal(texto, corTexto, corFundo);
    }

    private Color FundoEstadoAtencao() => DesktopPocMotion.Misturar(
        _paleta.Superficies.Painel,
        _paleta.Perigo,
        _tema == TemaDesktopPoc.Escuro ? 0.24D : 0.12D);

    private void DefinirEstadoGlobal(
        string texto,
        Color corTexto,
        Color corFundo,
        string? descricaoAcessivel = null)
    {
        _estadoGlobal.Text = texto;
        _estadoGlobal.ForeColor = corTexto;
        _estadoGlobal.BackColor = corFundo;
        _estadoGlobal.AccessibleDescription = descricaoAcessivel;
    }

    private void MostrarFalhaSegura(
        string mensagem,
        string titulo,
        Exception? exception = null,
        string operacao = "executar_acao_desktop")
    {
        if (IsDisposed)
        {
            return;
        }

        DefinirEstadoGlobal("Ação necessária", _paleta.Perigo, FundoEstadoAtencao());
        if (!_sessao.ModoDemonstracao && exception is not null)
        {
            _ = _sessao.RegistrarFalhaOperacionalAsync(
                operacao,
                exception,
                CancellationToken.None);
        }

        MessageBox.Show(
            mensagem,
            titulo,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    internal Task AbrirDetalheAgoraAsync(Guid reuniaoId, string abaInicial = "Resumo") =>
        AbrirDetalheAsync(reuniaoId, abaInicial);

    internal Task BuscarReunioesAgoraAsync(string? texto, string? status, string? periodo)
    {
        var versao = ++_versaoBuscaReunioes;
        return AtualizarListaReunioesAsync(texto, status, periodo, versao);
    }

    internal Task<string> ExportarAtaAgoraAsync(
        Guid reuniaoId,
        FormatoExportacaoAta formato,
        string caminhoDestino,
        bool sobrescrever) =>
        _sessao.ExportarAtaAsync(
            reuniaoId,
            formato,
            caminhoDestino,
            sobrescrever,
            _lifetime.Token);

    internal Task<string> PublicarAtaObsidianAgoraAsync(
        Guid reuniaoId,
        string caminhoVault,
        string subpasta) =>
        _sessao.PublicarAtaObsidianAsync(
            reuniaoId,
            caminhoVault,
            subpasta,
            _lifetime.Token);

    internal Task AtualizarAgoraAsync() => AtualizarDadosReaisAsync();

    internal void AbrirConfiguracoes() => Navegar("configuracoes");

    private static void AjustarLarguraFilhos(FlowLayoutPanel painel, int margem = 8)
    {
        var larguraScroll = painel.AutoScroll ? SystemInformation.VerticalScrollBarWidth : 0;
        var largura = Math.Max(100, painel.ClientSize.Width - margem - larguraScroll);
        foreach (Control control in painel.Controls)
        {
            control.Width = largura;
        }
    }

    private static void AplicarRegiaoArredondada(Control control, int raio)
    {
        void Atualizar()
        {
            using var caminho = new GraphicsPath();
            var diametro = raio * 2;
            caminho.AddArc(0, 0, diametro, diametro, 180, 90);
            caminho.AddArc(control.Width - diametro, 0, diametro, diametro, 270, 90);
            caminho.AddArc(control.Width - diametro, control.Height - diametro, diametro, diametro, 0, 90);
            caminho.AddArc(0, control.Height - diametro, diametro, diametro, 90, 90);
            caminho.CloseFigure();
            control.Region = new Region(caminho);
        }

        control.Resize += (_, _) => Atualizar();
        Atualizar();
    }
}

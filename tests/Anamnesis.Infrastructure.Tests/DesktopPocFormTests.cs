using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DesktopPocFormTests
{
    [Fact]
    public void RecuperacaoPendenteNaoDeveOferecerNovoInicioNemAfirmarObsAtivo()
    {
        ExecutarEmSta(() =>
        {
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new DesktopSessionRecuperacaoFake());
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            var botoesInicio = EncontrarControles(form).OfType<Button>().ToArray();

            Assert.DoesNotContain(botoesInicio, botao => botao.Text == "Iniciar gravação");
            var revisar = Assert.Single(
                botoesInicio,
                botao => botao.Text == "Revisar gravação anterior");
            revisar.PerformClick();
            System.Windows.Forms.Application.DoEvents();

            Assert.Contains(
                EncontrarLabels(form),
                label => label.Text.Contains(
                    "Nenhum comando foi enviado ao OBS",
                    StringComparison.Ordinal));
            Assert.Contains(
                EncontrarControles(form).OfType<Button>(),
                botao => botao.Text == "Encerrar gravação anterior");
        });
    }

    [Fact]
    public void ModoRealNaoDeveRenderizarDadosSimuladosEAtualizaAssincronamente()
    {
        ExecutarEmSta(() =>
        {
            var sessao = new DesktopSessionRealFake();
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                sessao);
            form.Show();
            AguardarInterface(() => sessao.Atualizacoes > 0);

            Assert.Contains(EncontrarLabels(form), label => label.Text == "DADOS LOCAIS REAIS");
            Assert.DoesNotContain(EncontrarLabels(form), label => label.Text.Contains("Planejamento do produto", StringComparison.Ordinal));
            CapturarQuandoSolicitado(form, "ANAMNESIS_REAL_DESKTOP_SCREENSHOT");

            EncontrarBotao(form, "Reuniões").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Contains(EncontrarLabels(form), label => label.Text == "0 reuniões persistidas localmente");

            EncontrarBotao(form, "Observabilidade").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.DoesNotContain(EncontrarLabels(form), label => label.Text == "TELEMETRIA SIMULADA");
            Assert.Contains(EncontrarLabels(form), label => label.Text == "EVENTOS LOCAIS PERSISTIDOS");
            Assert.Contains(EncontrarLabels(form), label => label.Text == "AO VIVO");
            Assert.Contains(EncontrarLabels(form), label => label.Text.Contains("job.reservado", StringComparison.Ordinal));
            Assert.Contains(
                EncontrarControles(form).OfType<DesktopSelectField>(),
                combo => combo.Text == "Últimas 24 horas");
            CapturarQuandoSolicitado(form, "ANAMNESIS_REAL_OBSERVABILITY_SCREENSHOT");
        });
    }

    [Fact]
    public void ModoRealDeveAtualizarDetalheQuandoSomenteJobMuda()
    {
        ExecutarEmSta(() =>
        {
            var reuniaoId = Guid.NewGuid();
            var sessao = new DesktopSessionDetalheFake(reuniaoId);
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                sessao);
            form.Show();
            AguardarInterface(() => sessao.Atualizacoes > 0);
            form.AbrirDetalheAgoraAsync(reuniaoId).GetAwaiter().GetResult();
            Assert.Contains(EncontrarLabels(form), label => label.Text.Contains("Job: Pendente", StringComparison.Ordinal));

            sessao.EstadoJob = "EmProcessamento";
            form.AtualizarAgoraAsync().GetAwaiter().GetResult();

            Assert.Contains(EncontrarLabels(form), label => label.Text.Contains("Job: EmProcessamento", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void DeveCancelarInicioPendenteAoDescartarJanela()
    {
        ExecutarEmSta(() =>
        {
            var sessao = new DesktopSessionInicioPendenteFake();
            var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                sessao);

            var inicio = form.IniciarGravacaoAgoraAsync();
            sessao.AguardarInicioAsync().GetAwaiter().GetResult();
            form.Dispose();
            AguardarInterface(() => inicio.IsCompleted);
            inicio.GetAwaiter().GetResult();

            Assert.True(sessao.CancelamentoObservado);
        });
    }

    [Fact]
    public void DeveNavegarEExecutarFluxoVisualSimulado()
    {
        ExecutarEmSta(() =>
        {
            System.Windows.Forms.Application.SetColorMode(SystemColorMode.Dark);
            var paleta = DesktopPocPalette.Criar(TemaDesktopPoc.Escuro);
            using var form = new DesktopPocForm(TemaDesktopPoc.Escuro);
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            Assert.Equal(paleta.Fundo, form.BackColor);
            Assert.Contains(EncontrarControles(form), control => control is DesktopBackdropPanel);
            Assert.Contains(EncontrarControles(form), control => control is DesktopSurfacePanel);
            Assert.Contains(EncontrarControles(form), control => control is DesktopActionButton);
            Assert.Contains(EncontrarControles(form), control => control is DesktopNavigationButton);
            Assert.All(
                EncontrarControles(form).Where(control => control is
                    DesktopBackdropPanel or
                    DesktopSurfacePanel or
                    DesktopActionButton or
                    DesktopNavigationButton or
                    DesktopTextField or
                    DesktopSelectField or
                    DesktopToggle),
                control => Assert.Equal(255, control.BackColor.A));
            var fundo = Assert.Single(EncontrarControles(form).OfType<DesktopBackdropPanel>());
            Assert.False(fundo.DecoracaoAtiva);
            Assert.False(fundo.AnimacaoContinuaAtiva);
            Assert.All(
                EncontrarControles(form).OfType<DesktopNavigationButton>(),
                botao => Assert.NotEqual(DesktopNavigationIcon.None, botao.Icon));
            Assert.Contains(EncontrarLabels(form), label => label.ForeColor == paleta.Texto);
            CapturarEvidenciaQuandoSolicitado(form);

            EncontrarBotao(form, "Reuniões").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Contains(EncontrarLabels(form), label => label.Text == "Reuniões");

            EncontrarBotao(form, "Observabilidade").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Contains(EncontrarLabels(form), label => label.Text == "Observabilidade");
            Assert.Contains(EncontrarLabels(form), label => label.Text == "TELEMETRIA SIMULADA");
            Assert.Contains(EncontrarLabels(form), label => label.Text.Contains("diagnostico.concluido", StringComparison.Ordinal));

            EncontrarBotao(form, "Início").PerformClick();
            EncontrarBotao(form, "Iniciar gravação").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Contains(EncontrarLabels(form), label => label.Text.Contains("GRAVANDO AGORA", StringComparison.Ordinal));
            CapturarQuandoSolicitado(form, "ANAMNESIS_POC_LIVE_SCREENSHOT");

            EncontrarBotao(form, "Encerrar e transcrever").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Contains(EncontrarLabels(form), label => label.Text == "Atividade");
            Assert.Contains(EncontrarLabels(form), label => label.Text.Contains("Whisper transcrevendo", StringComparison.Ordinal));

            EncontrarBotao(form, "Observabilidade").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Contains(EncontrarLabels(form), label => label.Text.Contains("job.criado", StringComparison.Ordinal));
            CapturarConsoleQuandoSolicitado(form);

            EncontrarBotao(form, "Configurações").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            Assert.Contains(EncontrarControles(form), control => control is DesktopTextField);
            Assert.Contains(EncontrarControles(form), control => control is DesktopSelectField);
            Assert.Contains(EncontrarControles(form), control => control is DesktopToggle);
            CapturarQuandoSolicitado(form, "ANAMNESIS_POC_SETTINGS_SCREENSHOT");
        });
    }

    [Fact]
    public void DeveManterConsoleLegivelNoTemaClaro()
    {
        ExecutarEmSta(() =>
        {
            var paleta = DesktopPocPalette.Criar(TemaDesktopPoc.Claro);
            using var form = new DesktopPocForm(TemaDesktopPoc.Claro);
            form.Show();
            EncontrarBotao(form, "Observabilidade").PerformClick();
            System.Windows.Forms.Application.DoEvents();

            Assert.Contains(EncontrarControles(form), control => control.BackColor == paleta.ConsoleFundo);
            Assert.Contains(
                EncontrarControles(form).OfType<DesktopSurfacePanel>(),
                superficie => superficie.Variant == DesktopSurfaceVariant.Navigation);
            Assert.Contains(
                EncontrarLabels(form),
                label => label.Text.Contains("diagnostico.concluido", StringComparison.Ordinal) &&
                         label.ForeColor == paleta.ConsoleTexto);
            CapturarQuandoSolicitado(form, "ANAMNESIS_POC_LIGHT_SCREENSHOT");
        });
    }

    [Fact]
    public void DeveRenderizarInterfaceSolidaSemMotion()
    {
        ExecutarEmSta(() =>
        {
            var politica = new DesktopPocEffectsPolicy(AnimacoesAtivas: false);
            using var form = new DesktopPocForm(TemaDesktopPoc.Escuro, politica);
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            var fundo = Assert.Single(EncontrarControles(form).OfType<DesktopBackdropPanel>());
            Assert.False(fundo.AnimacoesAtivas);
            Assert.All(
                EncontrarControles(form).OfType<DesktopSurfacePanel>(),
                superficie => Assert.Equal(255, superficie.BackColor.A));

            EncontrarBotao(form, "Reuniões").PerformClick();
            Assert.Contains(EncontrarLabels(form), label => label.Text == "Reuniões");
        });
    }

    [Fact]
    public void DeveConcluirTransicaoEspacialDePagina()
    {
        ExecutarEmSta(() =>
        {
            var politica = new DesktopPocEffectsPolicy(AnimacoesAtivas: true);
            using var form = new DesktopPocForm(TemaDesktopPoc.Escuro, politica);
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            EncontrarBotao(form, "Reuniões").PerformClick();
            var titulo = EncontrarLabels(form).Single(label => label.Text == "Reuniões");
            var pagina = titulo.Parent!.Parent!;
            var esquerdaFinal = pagina.Parent!.DisplayRectangle.Left;
            Assert.Equal(DockStyle.None, pagina.Dock);
            Assert.Equal(esquerdaFinal + DesktopPocDesignTokens.Padrao.Motion.DeslocamentoPagina, pagina.Left);

            var relogio = Stopwatch.StartNew();
            while (relogio.Elapsed < TimeSpan.FromMilliseconds(400))
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Yield();
            }

            Assert.Equal(DockStyle.Fill, pagina.Dock);
            Assert.Equal(esquerdaFinal, pagina.Left);
        });
    }

    private static Button EncontrarBotao(Control raiz, string texto) =>
        EncontrarControles(raiz)
            .OfType<Button>()
            .Single(botao => botao.Text.Contains(texto, StringComparison.Ordinal));

    private static IEnumerable<Label> EncontrarLabels(Control raiz) =>
        EncontrarControles(raiz).OfType<Label>();

    private static IEnumerable<Control> EncontrarControles(Control raiz)
    {
        foreach (Control control in raiz.Controls)
        {
            yield return control;
            foreach (var filho in EncontrarControles(control))
            {
                yield return filho;
            }
        }
    }

    private static void CapturarEvidenciaQuandoSolicitado(Form form) =>
        CapturarQuandoSolicitado(form, "ANAMNESIS_POC_SCREENSHOT");

    private static void CapturarConsoleQuandoSolicitado(Form form) =>
        CapturarQuandoSolicitado(form, "ANAMNESIS_POC_OBSERVABILITY_SCREENSHOT");

    private static void CapturarQuandoSolicitado(Form form, string variavel)
    {
        var caminho = Environment.GetEnvironmentVariable(variavel);
        if (string.IsNullOrWhiteSpace(caminho))
        {
            return;
        }

        caminho = Path.GetFullPath(caminho);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        using var imagem = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(imagem, new Rectangle(Point.Empty, form.ClientSize));
        imagem.Save(caminho, System.Drawing.Imaging.ImageFormat.Png);
    }

    private static void ExecutarEmSta(Action acao)
    {
        Exception? falha = null;
        var thread = new Thread(() =>
        {
            try
            {
                acao();
            }
            catch (Exception exception)
            {
                falha = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException("A validação da janela desktop não terminou em quinze segundos.");
        }

        if (falha is not null)
        {
            ExceptionDispatchInfo.Capture(falha).Throw();
        }
    }

    private static void AguardarInterface(Func<bool> condicao)
    {
        var relogio = Stopwatch.StartNew();
        while (!condicao() && relogio.Elapsed < TimeSpan.FromSeconds(2))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }

        Assert.True(condicao());
    }

    private sealed class DesktopSessionRealFake : IDesktopSession
    {
        public bool ModoDemonstracao => false;
        public EtapaDesktopPoc Etapa => EtapaDesktopPoc.Pronto;
        public TimeSpan DuracaoGravacao => TimeSpan.Zero;
        public IReadOnlyList<ReuniaoDesktopPoc> Reunioes => [];
        public IReadOnlyList<EventoObservabilidadePoc> EventosOperacionais { get; } =
        [
            new EventoObservabilidadePoc(
                DateTimeOffset.Now,
                NivelEventoPoc.Info,
                "Worker",
                "job.reservado",
                "Job real reservado.",
                "r:01 j:02",
                12)
        ];
        public int JobsNaFila => 1;
        public int Atualizacoes { get; private set; }

        public Task AtualizarAsync(CancellationToken cancellationToken)
        {
            Atualizacoes++;
            return Task.CompletedTask;
        }

        public Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken) => Task.CompletedTask;

        public void AvancarGravacao()
        {
        }

        public Task EncerrarGravacaoAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void ConcluirProcessamentoSimulado()
        {
        }

        public Task<ReuniaoDesktopPoc?> ObterDetalheAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<ReuniaoDesktopPoc?>(null);

        public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DesktopSessionRecuperacaoFake : IDesktopSession
    {
        public bool ModoDemonstracao => false;
        public EtapaDesktopPoc Etapa => EtapaDesktopPoc.Gravando;
        public TimeSpan DuracaoGravacao => TimeSpan.FromMinutes(5);
        public bool RecuperacaoPendente => true;
        public IReadOnlyList<ReuniaoDesktopPoc> Reunioes { get; } =
        [
            new ReuniaoDesktopPoc
            {
                Id = Guid.NewGuid(),
                Titulo = "Reunião anterior",
                Data = "Agora",
                Plataforma = "Captura OBS",
                Duracao = "00:05:00",
                Status = "Gravando",
                Resumo = "Recuperação pendente.",
                PontosPrincipais = [],
                Transcricao = [],
                Decisoes = [],
                Tarefas = []
            }
        ];

        public Task AtualizarAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Novo início não deve ser oferecido.");
        public void AvancarGravacao() { }
        public Task EncerrarGravacaoAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void ConcluirProcessamentoSimulado() { }
        public Task<ReuniaoDesktopPoc?> ObterDetalheAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<ReuniaoDesktopPoc?>(null);
        public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DesktopSessionInicioPendenteFake : IDesktopSession
    {
        private readonly TaskCompletionSource _inicioAcionado = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ModoDemonstracao => false;
        public EtapaDesktopPoc Etapa => EtapaDesktopPoc.Pronto;
        public TimeSpan DuracaoGravacao => TimeSpan.Zero;
        public IReadOnlyList<ReuniaoDesktopPoc> Reunioes => [];
        public bool CancelamentoObservado { get; private set; }

        public Task AtualizarAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken)
        {
            _inicioAcionado.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancelamentoObservado = true;
                throw;
            }
        }

        public Task AguardarInicioAsync() => _inicioAcionado.Task;

        public void AvancarGravacao()
        {
        }

        public Task EncerrarGravacaoAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void ConcluirProcessamentoSimulado()
        {
        }

        public Task<ReuniaoDesktopPoc?> ObterDetalheAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<ReuniaoDesktopPoc?>(null);

        public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DesktopSessionDetalheFake(Guid reuniaoId) : IDesktopSession
    {
        public bool ModoDemonstracao => false;
        public EtapaDesktopPoc Etapa => EtapaDesktopPoc.Processando;
        public TimeSpan DuracaoGravacao => TimeSpan.Zero;
        public int Atualizacoes { get; private set; }
        public string EstadoJob { get; set; } = "Pendente";
        public IReadOnlyList<ReuniaoDesktopPoc> Reunioes { get; } =
        [
            CriarReuniao(reuniaoId, ["Job: Pendente"])
        ];

        public Task AtualizarAsync(CancellationToken cancellationToken)
        {
            Atualizacoes++;
            return Task.CompletedTask;
        }

        public Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken) => Task.CompletedTask;

        public void AvancarGravacao()
        {
        }

        public Task EncerrarGravacaoAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void ConcluirProcessamentoSimulado()
        {
        }

        public Task<ReuniaoDesktopPoc?> ObterDetalheAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<ReuniaoDesktopPoc?>(
                id == reuniaoId ? CriarReuniao(reuniaoId, [$"Job: {EstadoJob}"]) : null);

        public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;

        private static ReuniaoDesktopPoc CriarReuniao(Guid id, IReadOnlyList<string> pontos) => new()
        {
            Id = id,
            Titulo = "Reunião real",
            Data = "05/08/2026 14:00",
            Plataforma = "Captura OBS",
            Duracao = "00:30:00",
            Status = "Transcrevendo",
            Resumo = "Resumo real.",
            PontosPrincipais = pontos,
            Transcricao = [],
            Decisoes = [],
            Tarefas = []
        };
    }
}

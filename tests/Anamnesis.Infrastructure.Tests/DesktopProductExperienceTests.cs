using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

[Collection("InterfaceWindowsGrupo")]
public sealed class DesktopProductExperienceTests
{
    [Fact]
    public void AberturaInterativaDeveExibirMarcaESuprimirModosSilenciosos()
    {
        Assert.True(DesktopStartupExperience.DeveExibirSplash([]));
        Assert.False(DesktopStartupExperience.DeveExibirSplash(["--background"]));
        Assert.False(DesktopStartupExperience.DeveExibirSplash(["--diagnostico-deteccao"]));
        Assert.False(DesktopStartupExperience.DeveExibirSplash(["--gravar-teste-segundos", "2"]));
    }

    [Fact]
    public void PrimeiroAcessoDevePermanecerPendenteAteConclusao()
    {
        var pendente = ConfiguracaoAnamnesis.CriarPadrao() with { PrimeiroUsoConcluido = false };
        var concluido = pendente with { PrimeiroUsoConcluido = true };

        Assert.True(DesktopStartupExperience.DeveExibirWizard(pendente, []));
        Assert.False(DesktopStartupExperience.DeveExibirWizard(concluido, []));
        Assert.False(DesktopStartupExperience.DeveExibirWizard(pendente, ["--background"]));
    }

    [Fact]
    public void SplashDeveUsarMarcaVetorialEEstadoFinalSemAnimacao()
    {
        ExecutarEmSta(() =>
        {
            using var splash = new DesktopSplashForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false));
            var marca = Assert.Single(EncontrarControles(splash).OfType<DesktopBrandMark>());

            Assert.Equal("Anamnesis iniciando", splash.AccessibleName);
            Assert.Equal(1D, marca.Progresso);
            Assert.Equal("Marca Anamnesis", marca.AccessibleName);
        });
    }

    [Fact]
    public void RascunhoDeveValidarEntradaEPreservarConfiguracaoAvancada()
    {
        var original = ConfiguracaoAnamnesis.CriarPadrao() with
        {
            SenhaObs = "segredo-protegido-em-memoria",
            CaminhoExecutavelWhisper = @"C:\ferramentas\whisper.exe",
            ArgumentosCli = ["exec", "--json"]
        };
        var rascunho = DesktopConfigurationDraft.From(original, iniciarComWindows: false) with
        {
            DiretorioArquivo = @"C:\Reunioes Anamnesis",
            EnderecoObs = "ws://127.0.0.1:4455",
            NomeCli = "Codex CLI",
            CaminhoExecutavelCli = @"C:\ferramentas\codex.exe"
        };

        var atualizada = rascunho.Build(original);

        Assert.Equal(@"C:\Reunioes Anamnesis", atualizada.DiretorioArquivo);
        Assert.Equal("segredo-protegido-em-memoria", atualizada.SenhaObs);
        Assert.Equal(@"C:\ferramentas\whisper.exe", atualizada.CaminhoExecutavelWhisper);
        Assert.Equal(["exec", "--json"], atualizada.ArgumentosCli);
        Assert.True(atualizada.PrimeiroUsoConcluido);
    }

    [Theory]
    [InlineData("", "ws://127.0.0.1:4455")]
    [InlineData("C:\\Arquivo", "http://127.0.0.1:4455")]
    public void RascunhoInvalidoNaoDeveProduzirConfiguracao(string diretorio, string enderecoObs)
    {
        var original = ConfiguracaoAnamnesis.CriarPadrao();
        var rascunho = DesktopConfigurationDraft.From(original, false) with
        {
            DiretorioArquivo = diretorio,
            EnderecoObs = enderecoObs
        };

        Assert.Throws<InvalidOperationException>(() => rascunho.Build(original));
    }

    [Fact]
    public void WizardDeveSerCurtoAcessivelEConcluirSomenteNoTerceiroPasso()
    {
        ExecutarEmSta(() =>
        {
            using var form = new PrimeiroAcessoForm(
                ConfiguracaoAnamnesis.CriarPadrao() with { PrimeiroUsoConcluido = false },
                iniciarComWindows: false,
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false));

            Assert.Equal(3, form.TotalPassos);
            Assert.Equal(1, form.PassoAtual);
            Assert.Equal("Configuração inicial do Anamnesis", form.AccessibleName);
            Assert.Contains(EncontrarControles(form), controle => controle is DesktopBrandMark);

            form.AvancarAgora();
            Assert.Equal(2, form.PassoAtual);
            CapturarQuandoSolicitado(form, "ANAMNESIS_FIRST_RUN_SCREENSHOT");
            form.AvancarAgora();
            Assert.Equal(3, form.PassoAtual);
            form.AvancarAgora();

            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.True(form.Configuracao.PrimeiroUsoConcluido);
        });
    }

    [Fact]
    public void BotoesDeGravacaoDevemUsarIconesVetoriaisDistintos()
    {
        ExecutarEmSta(() =>
        {
            using var form = new DesktopPocForm(TemaDesktopPoc.Escuro);
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            var iniciar = EncontrarControles(form)
                .OfType<DesktopActionButton>()
                .Single(botao => botao.Text == "Iniciar gravação");
            Assert.Equal(DesktopActionIcon.Record, iniciar.Icon);

            form.IniciarGravacaoAgoraAsync().GetAwaiter().GetResult();
            System.Windows.Forms.Application.DoEvents();
            var encerrar = EncontrarControles(form)
                .OfType<DesktopActionButton>()
                .Single(botao => botao.Text == "Encerrar e transcrever");
            Assert.Equal(DesktopActionIcon.Stop, encerrar.Icon);

            EncontrarControles(form).OfType<Button>().Single(botao => botao.Text == "Início").PerformClick();
            var acompanhar = EncontrarControles(form)
                .OfType<DesktopActionButton>()
                .Single(botao => botao.Text == "Ver gravação ao vivo");
            Assert.Equal(DesktopActionIcon.Live, acompanhar.Icon);
        });
    }

    [Fact]
    public void FormularioRealDeveSalvarValoresEditadosEPreservarSegredo()
    {
        ExecutarEmSta(() =>
        {
            var original = ConfiguracaoAnamnesis.CriarPadrao() with
            {
                PrimeiroUsoConcluido = true,
                SenhaObs = "segredo-em-memoria",
                CaminhoExecutavelWhisper = @"C:\local\whisper.exe"
            };
            ConfiguracaoAnamnesis? salva = null;
            var inicializacao = false;
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new DesktopConfigurationSessionFake(original),
                criarLembrete: null,
                salvarConfiguracao: (configuracao, _) =>
                {
                    salva = configuracao;
                    return Task.CompletedTask;
                },
                obterInicializacaoWindows: () => false,
                definirInicializacaoWindows: ativa => inicializacao = ativa);
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            EncontrarControles(form).OfType<Button>().Single(botao => botao.Text == "Configurações").PerformClick();

            EncontrarControles(form).OfType<DesktopTextField>()
                .Single(campo => campo.AccessibleName == "Pasta das reuniões").Text = @"C:\Reunioes Produto";
            EncontrarControles(form).OfType<DesktopTextField>()
                .Single(campo => campo.AccessibleName == "Endereço local do OBS").Text = "ws://localhost:4455";
            EncontrarControles(form).OfType<DesktopToggle>()
                .Single(toggle => toggle.AccessibleName == "Iniciar Anamnesis com o Windows").Checked = true;

            var salvar = EncontrarControles(form).OfType<DesktopActionButton>()
                .Single(botao => botao.Text == "Salvar alterações");
            Assert.Equal(DesktopActionIcon.Save, salvar.Icon);
            salvar.PerformClick();
            AguardarInterface(() => salva is not null);

            Assert.Equal(@"C:\Reunioes Produto", salva!.DiretorioArquivo);
            Assert.Equal("ws://localhost:4455", salva.EnderecoObs);
            Assert.Equal("segredo-em-memoria", salva.SenhaObs);
            Assert.Equal(@"C:\local\whisper.exe", salva.CaminhoExecutavelWhisper);
            Assert.True(inicializacao);
        });
    }

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "A validação da experiência visual não terminou em quinze segundos.");
        if (falha is not null)
        {
            throw falha;
        }
    }

    private static void AguardarInterface(Func<bool> condicao)
    {
        var inicio = DateTime.UtcNow;
        while (!condicao() && DateTime.UtcNow - inicio < TimeSpan.FromSeconds(5))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.True(condicao());
    }

    private static void CapturarQuandoSolicitado(Form form, string variavel)
    {
        var caminho = Environment.GetEnvironmentVariable(variavel);
        if (string.IsNullOrWhiteSpace(caminho))
        {
            return;
        }

        caminho = Path.GetFullPath(caminho);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        form.Show();
        System.Windows.Forms.Application.DoEvents();
        using var imagem = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(imagem, new Rectangle(Point.Empty, form.ClientSize));
        imagem.Save(caminho, System.Drawing.Imaging.ImageFormat.Png);
    }

    private sealed class DesktopConfigurationSessionFake : IDesktopSession
    {
        public DesktopConfigurationSessionFake(ConfiguracaoAnamnesis configuracao)
        {
            Ambiente = new DesktopRuntimeInfo(
                @"C:\local\Anamnesis\config.json",
                configuracao.CaminhoBanco,
                configuracao.DiretorioArquivo,
                configuracao.NomeCli,
                [new ProntidaoDesktopItem("OBS", true, "Pronto")],
                configuracao);
        }

        public bool ModoDemonstracao => false;
        public EtapaDesktopPoc Etapa => EtapaDesktopPoc.Pronto;
        public TimeSpan DuracaoGravacao => TimeSpan.Zero;
        public IReadOnlyList<ReuniaoDesktopPoc> Reunioes => [];
        public DesktopRuntimeInfo Ambiente { get; }

        public Task AtualizarAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken) => Task.CompletedTask;
        public void AvancarGravacao() { }
        public Task EncerrarGravacaoAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void ConcluirProcessamentoSimulado() { }
        public Task<ReuniaoDesktopPoc?> ObterDetalheAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<ReuniaoDesktopPoc?>(null);
        public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

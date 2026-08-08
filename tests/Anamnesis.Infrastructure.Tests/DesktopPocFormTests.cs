using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Anamnesis.Application.Modelos;
using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

[Collection(InterfaceWindowsGrupo.Nome)]
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
    public void ModoRealDeveUsarLarguraUtilNasConfiguracoesLocais()
    {
        ExecutarEmSta(() =>
        {
            var paleta = DesktopPocPalette.Criar(TemaDesktopPoc.Escuro);
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new DesktopSessionRealFake());
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            EncontrarBotao(form, "Configurações").PerformClick();
            System.Windows.Forms.Application.DoEvents();

            var titulo = EncontrarLabels(form)
                .Single(label => label.Text == "Configuração local em uso");
            var bloco = Assert.IsType<DesktopShadowPanel>(titulo.Parent!.Parent!.Parent);

            Assert.True(bloco.Width >= form.ClientSize.Width / 2);
            Assert.All(
                EncontrarLabels(bloco),
                label => Assert.True(label.Right <= bloco.ClientSize.Width - bloco.Padding.Right));
            Assert.Contains(
                EncontrarLabels(form),
                label => label.Text.StartsWith("Atenção:", StringComparison.Ordinal) &&
                         label.ForeColor == paleta.Destaque);
            CapturarQuandoSolicitado(form, "ANAMNESIS_REAL_SETTINGS_SCREENSHOT");
        });
    }

    [Fact]
    public void ModoRealDeveManterDetalheAbertoNaAbaSelecionadaQuandoJobMuda()
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
            EncontrarBotao(form, "Transcrição").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            var tituloAntes = EncontrarLabels(form)
                .Single(label => label.Text == "Conteúdo transcrito");
            var paginaAntes = tituloAntes.Parent!.Parent!;

            sessao.EstadoJob = "EmProcessamento";
            form.AtualizarAgoraAsync().GetAwaiter().GetResult();

            var tituloDepois = EncontrarLabels(form)
                .Single(label => label.Text == "Conteúdo transcrito");
            Assert.Same(paginaAntes, tituloDepois.Parent!.Parent!);
        });
    }

    [Fact]
    public void DetalheDeveExibirTextoSelecionavelSomenteLeituraEAcaoDeCopia()
    {
        ExecutarEmSta(() =>
        {
            var reuniaoId = Guid.NewGuid();
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new DesktopSessionDetalheFake(reuniaoId));
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            form.AbrirDetalheAgoraAsync(reuniaoId, "Transcrição").GetAwaiter().GetResult();

            var texto = Assert.Single(EncontrarControles(form).OfType<RichTextBox>());
            Assert.True(texto.ReadOnly);
            Assert.True(texto.ShortcutsEnabled);
            Assert.True(texto.TabStop);
            Assert.Contains("Pessoa 1: decisão importante.", texto.Text, StringComparison.Ordinal);
            texto.Select(0, 8);
            Assert.Equal("Pessoa 1", texto.SelectedText);
            Assert.NotNull(EncontrarBotao(form, "Copiar texto"));
        });
    }

    [Fact]
    public void CliqueNoTituloDoCartaoDeveAbrirDetalheDaReuniao()
    {
        ExecutarEmSta(() =>
        {
            var reuniaoId = Guid.NewGuid();
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new DesktopSessionDetalheFake(reuniaoId));
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            EncontrarBotao(form, "Reuniões").PerformClick();
            System.Windows.Forms.Application.DoEvents();

            DispararClique(EncontrarLabels(form).Single(label => label.Text == "Reunião real"));
            AguardarInterface(() => EncontrarControles(form).OfType<Button>()
                .Any(botao => botao.Text == "Transcrição"));

            Assert.NotNull(EncontrarBotao(form, "Transcrição"));
        });
    }

    [Fact]
    public void BuscaCompletaDeveExibirTrechoEAbrirSecaoCorrespondente()
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
            System.Windows.Forms.Application.DoEvents();
            EncontrarBotao(form, "Reuniões").PerformClick();
            System.Windows.Forms.Application.DoEvents();

            form.BuscarReunioesAgoraAsync(
                    "incidente resolvido",
                    "Todos os estados",
                    "Todo o período")
                .GetAwaiter()
                .GetResult();
            System.Windows.Forms.Application.DoEvents();

            Assert.Equal("incidente resolvido", sessao.UltimoTextoBusca);
            Assert.Contains(
                EncontrarLabels(form),
                label => label.Text.Contains("incidente resolvido", StringComparison.OrdinalIgnoreCase));
            CapturarQuandoSolicitado(form, "ANAMNESIS_SEARCH_SCREENSHOT");
            DispararClique(EncontrarLabels(form).Single(label => label.Text == "Reunião real"));
            AguardarInterface(() => EncontrarLabels(form)
                .Any(label => label.Text == "Conteúdo transcrito"));
        });
    }

    [Fact]
    public void TarefaDeveOferecerCriacaoDeLembreteConfirmado()
    {
        ExecutarEmSta(() =>
        {
            var reuniaoId = Guid.NewGuid();
            Guid? reuniaoRecebida = null;
            string? tarefaRecebida = null;
            DateTimeOffset? horarioRecebido = null;
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new DesktopSessionDetalheFake(reuniaoId),
                (id, tarefa, horario, _) =>
                {
                    reuniaoRecebida = id;
                    tarefaRecebida = tarefa;
                    horarioRecebido = horario;
                    return Task.CompletedTask;
                });
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            form.AbrirDetalheAgoraAsync(reuniaoId, "Tarefas").GetAwaiter().GetResult();

            Assert.NotNull(EncontrarBotao(form, "Criar lembrete"));
            var horario = DateTimeOffset.Now.AddDays(1);
            form.CriarLembreteConfirmadoAgoraAsync(
                    reuniaoId,
                    "Felipe: concluir atividade.",
                    horario)
                .GetAwaiter()
                .GetResult();

            Assert.Equal(reuniaoId, reuniaoRecebida);
            Assert.Equal("Felipe: concluir atividade.", tarefaRecebida);
            Assert.Equal(horario, horarioRecebido);
        });
    }

    [Fact]
    public void TelaReunioesDeveOrganizarBuscaFiltrosEContagemDeResultados()
    {
        ExecutarEmSta(() =>
        {
            var reuniaoId = Guid.NewGuid();
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new DesktopSessionDetalheFake(reuniaoId));
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            EncontrarBotao(form, "Reuniões").PerformClick();
            System.Windows.Forms.Application.DoEvents();

            Assert.Contains(EncontrarLabels(form), label => label.Text == "BUSCAR E FILTRAR");
            Assert.Contains(EncontrarLabels(form), label => label.Text == "HISTÓRICO LOCAL");
            Assert.Contains(EncontrarLabels(form), label => label.Text == "1 reunião encontrada");

            var busca = Assert.Single(EncontrarControles(form).OfType<DesktopTextField>());
            Assert.Equal(DockStyle.Fill, busca.Dock);
            Assert.Equal("Buscar reuniões", busca.AccessibleName);
            Assert.All(
                EncontrarControles(form).OfType<DesktopSelectField>(),
                filtro => Assert.Equal(DockStyle.Fill, filtro.Dock));
            CapturarQuandoSolicitado(form, "ANAMNESIS_MEETINGS_SCREENSHOT");
        });
    }

    [Fact]
    public void ReuniaoNaListaDeveSerContinuaAcessivelEAbrirPorTeclado()
    {
        ExecutarEmSta(() =>
        {
            var reuniaoId = Guid.NewGuid();
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new DesktopSessionDetalheFake(reuniaoId));
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            EncontrarBotao(form, "Reuniões").PerformClick();
            System.Windows.Forms.Application.DoEvents();

            var item = Assert.Single(EncontrarControles(form).OfType<DesktopReuniaoListItem>());
            Assert.True(item.TabStop);
            Assert.Equal(AccessibleRole.ListItem, item.AccessibleRole);
            Assert.Null(item.Region);
            Assert.Equal(0, item.Margin.Bottom);

            typeof(Control)
                .GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(item, [new KeyEventArgs(Keys.Enter)]);
            AguardarInterface(() => EncontrarLabels(form).Any(label => label.Text == "DETALHES DA REUNIÃO"));
        });
    }

    [Fact]
    public void DetalheDeveUsarAbasContinuasComUmaSelecaoClara()
    {
        ExecutarEmSta(() =>
        {
            var reuniaoId = Guid.NewGuid();
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new DesktopSessionDetalheFake(reuniaoId));
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            form.AbrirDetalheAgoraAsync(reuniaoId, "Decisões").GetAwaiter().GetResult();

            var abas = EncontrarControles(form).OfType<DesktopTabButton>().ToArray();
            Assert.Equal(5, abas.Length);
            Assert.All(abas, aba =>
            {
                Assert.Null(aba.Region);
                Assert.Equal(AccessibleRole.PageTab, aba.AccessibleRole);
                Assert.True(aba.Height >= 42);
            });
            Assert.Single(abas, aba => aba.Selecionado);
            Assert.True(abas.Single(aba => aba.Text == "Decisões").Selecionado);
        });
    }

    [Fact]
    public void DetalheDeveTerHierarquiaEditorialETextoConfortavel()
    {
        ExecutarEmSta(() =>
        {
            var reuniaoId = Guid.NewGuid();
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                new DesktopSessionDetalheFake(reuniaoId));
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            EncontrarBotao(form, "Reuniões").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            form.AbrirDetalheAgoraAsync(reuniaoId, "Decisões").GetAwaiter().GetResult();

            Assert.Contains(EncontrarLabels(form), label => label.Text == "DETALHES DA REUNIÃO");
            Assert.Contains(EncontrarLabels(form), label => label.Text == "O que ficou decidido");
            Assert.Contains(EncontrarLabels(form), label => label.Text == "Transcrevendo");

            var texto = Assert.Single(EncontrarControles(form).OfType<RichTextBox>());
            Assert.True(texto.Font.Size >= 10.5F);
            var bloco = Assert.IsType<DesktopShadowPanel>(texto.Parent!.Parent);
            Assert.True(bloco.Height <= 220);
            CapturarQuandoSolicitado(form, "ANAMNESIS_DETAIL_SCREENSHOT");
        });
    }

    [Fact]
    public void DetalheDeveOferecerExportacaoEPublicacaoLocal()
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
            System.Windows.Forms.Application.DoEvents();
            form.AbrirDetalheAgoraAsync(reuniaoId, "Arquivos").GetAwaiter().GetResult();

            Assert.NotNull(EncontrarBotao(form, "Exportar PDF"));
            Assert.NotNull(EncontrarBotao(form, "Exportar DOCX"));
            Assert.NotNull(EncontrarBotao(form, "Publicar no Obsidian"));
            CapturarQuandoSolicitado(form, "ANAMNESIS_EXPORT_SCREENSHOT");
            form.ExportarAtaAgoraAsync(
                    reuniaoId,
                    FormatoExportacaoAta.Pdf,
                    @"C:\destino\ata.pdf",
                    sobrescrever: true)
                .GetAwaiter()
                .GetResult();
            form.PublicarAtaObsidianAgoraAsync(
                    reuniaoId,
                    @"C:\vault",
                    "Anamnesis/Reunioes")
                .GetAwaiter()
                .GetResult();

            Assert.Equal(FormatoExportacaoAta.Pdf, sessao.UltimoFormatoExportado);
            Assert.Equal(@"C:\destino\ata.pdf", sessao.UltimoDestinoExportado);
            Assert.Equal(@"C:\vault", sessao.UltimoVaultPublicado);
        });
    }

    [Fact]
    public void FluxoManualDeveExibirProgressoFiltrarLogsEAbrirTranscricaoAoConcluir()
    {
        ExecutarEmSta(() =>
        {
            var sessao = new DesktopSessionFluxoManualFake();
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                sessao);
            form.Show();
            AguardarInterface(() => sessao.Atualizacoes > 0);

            form.IniciarGravacaoSeTituloConfirmadoAgoraAsync(null).GetAwaiter().GetResult();
            Assert.Null(sessao.UltimoTitulo);

            form.IniciarGravacaoComTituloAgoraAsync("  Planejamento da alpha  ").GetAwaiter().GetResult();
            Assert.Equal("Planejamento da alpha", sessao.UltimoTitulo);

            form.EncerrarGravacaoAgoraAsync().GetAwaiter().GetResult();
            Assert.Contains(EncontrarLabels(form), label => label.Text.Contains("Planejamento da alpha", StringComparison.Ordinal));
            Assert.Contains(EncontrarLabels(form), label => label.Text.Contains("Whisper transcrevendo", StringComparison.Ordinal));
            var barra = Assert.Single(EncontrarControles(form).OfType<DesktopProgressBar>());
            Assert.True(barra.MarqueeAtivo);
            CapturarQuandoSolicitado(form, "ANAMNESIS_POC_PROCESSING_SCREENSHOT");

            EncontrarBotao(form, "Ver console de logs").PerformClick();
            System.Windows.Forms.Application.DoEvents();
            var busca = Assert.Single(EncontrarControles(form).OfType<DesktopTextField>());
            Assert.Equal($"r:{sessao.ReuniaoId:N}", busca.Text);
            Assert.Contains(EncontrarLabels(form), label => label.Text.Contains("worker.transcricao", StringComparison.Ordinal));
            Assert.DoesNotContain(EncontrarLabels(form), label => label.Text.Contains("evento.de.outra.reuniao", StringComparison.Ordinal));

            form.AtualizarAgoraAsync().GetAwaiter().GetResult();
            Assert.Same(busca, Assert.Single(EncontrarControles(form).OfType<DesktopTextField>()));
            Assert.Equal($"r:{sessao.ReuniaoId:N}", busca.Text);

            EncontrarBotao(form, "Atividade").PerformClick();
            sessao.Concluir();
            form.AtualizarAgoraAsync().GetAwaiter().GetResult();

            EncontrarBotao(form, "Abrir transcrição").PerformClick();
            AguardarInterface(() => EncontrarLabels(form).Any(label => label.Text == "Conteúdo transcrito"));
            Assert.Contains(
                EncontrarControles(form).OfType<RichTextBox>(),
                texto => texto.Text.Contains("Transcrição final da reunião manual", StringComparison.Ordinal));
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
            Assert.NotNull(EncontrarBotao(form, "Iniciar gravação"));
            form.IniciarGravacaoAgoraAsync().GetAwaiter().GetResult();
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
    public void NavegacaoComMotionAtivoDeveTrocarPaginaSemDeslocarConteudo()
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
            var conteudo = pagina.Parent!;

            Assert.Single(conteudo.Controls.Cast<Control>());
            Assert.Equal(DockStyle.Fill, pagina.Dock);
            Assert.Equal(conteudo.DisplayRectangle.Left, pagina.Left);
            Assert.Equal(conteudo.DisplayRectangle.Top, pagina.Top);
        });
    }

    [Fact]
    public void NavegacaoRapidaComAnimacaoDeveConcluirSomenteNaUltimaPagina()
    {
        ExecutarEmSta(() =>
        {
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: true),
                new DesktopSessionRealFake());
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            EncontrarBotao(form, "Reuniões").PerformClick();
            EncontrarBotao(form, "Observabilidade").PerformClick();

            var conteudo = Assert.IsType<Panel>(typeof(DesktopPocForm)
                .GetField("_conteudo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(form));
            AguardarInterface(() =>
                conteudo.Controls.Count == 1 &&
                conteudo.Controls[0].Dock == DockStyle.Fill &&
                EncontrarLabels(conteudo.Controls[0]).Any(label => label.Text == "Observabilidade"));

            var pagina = Assert.Single(conteudo.Controls.Cast<Control>());
            Assert.Equal(conteudo.DisplayRectangle.Left, pagina.Left);
            Assert.DoesNotContain(EncontrarLabels(pagina), label => label.Text == "Reuniões");
        });
    }

    [Fact]
    public void TrocaDeMenuNaoDeveAlterarGeometriaDaNavegacaoLateral()
    {
        ExecutarEmSta(() =>
        {
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: true));
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            var geometriaInicial = EncontrarControles(form)
                .OfType<DesktopNavigationButton>()
                .ToDictionary(botao => botao.Text, botao => botao.Bounds);

            EncontrarBotao(form, "Reuniões").PerformClick();
            EncontrarBotao(form, "Atividade").PerformClick();
            EncontrarBotao(form, "Observabilidade").PerformClick();

            Assert.All(
                EncontrarControles(form).OfType<DesktopNavigationButton>(),
                botao => Assert.Equal(geometriaInicial[botao.Text], botao.Bounds));
        });
    }

    [Fact]
    public void NavegacaoLateralDeveReservarFolgaParaPinturaDosCantos()
    {
        ExecutarEmSta(() =>
        {
            using var form = new DesktopPocForm(TemaDesktopPoc.Escuro);
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            Assert.All(
                EncontrarControles(form).OfType<DesktopNavigationButton>(),
                botao => Assert.True(
                    botao.Right <= botao.Parent!.ClientSize.Width - 2,
                    $"'{botao.Text}' termina em {botao.Right}px para {botao.Parent.ClientSize.Width}px disponíveis."));
        });
    }

    [Fact]
    public void EstadoDeAtencaoNaoDeveReutilizarFundoVerdeDeSucesso()
    {
        ExecutarEmSta(() =>
        {
            var sessao = new DesktopSessionRealFake(incluirFalha: true);
            var paleta = DesktopPocPalette.Criar(TemaDesktopPoc.Escuro);
            using var form = new DesktopPocForm(
                TemaDesktopPoc.Escuro,
                new DesktopPocEffectsPolicy(AnimacoesAtivas: false),
                sessao);
            form.Show();
            AguardarInterface(() => sessao.Atualizacoes > 0);

            var estado = EncontrarLabels(form).Single(label => label.Text == "Ação necessária");
            Assert.NotEqual(paleta.FundoPositivo, estado.BackColor);
        });
    }

    private static Button EncontrarBotao(Control raiz, string texto) =>
        EncontrarControles(raiz)
            .OfType<Button>()
            .Single(botao => botao.Text.Contains(texto, StringComparison.Ordinal));

    private static void DispararClique(Control controle) =>
        typeof(Control)
            .GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(controle, [EventArgs.Empty]);

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
        public DesktopSessionRealFake(bool incluirFalha = false)
        {
            Reunioes = incluirFalha
                ?
                [
                    new ReuniaoDesktopPoc
                    {
                        Id = Guid.NewGuid(),
                        Titulo = "Reunião com falha",
                        Data = "Agora",
                        Plataforma = "Captura OBS",
                        Duracao = "00:01:00",
                        Status = "Falha",
                        Resumo = "Falha pendente.",
                        PontosPrincipais = [],
                        Transcricao = [],
                        Decisoes = [],
                        Tarefas = []
                    }
                ]
                : [];
        }

        public bool ModoDemonstracao => false;
        public EtapaDesktopPoc Etapa => EtapaDesktopPoc.Pronto;
        public TimeSpan DuracaoGravacao => TimeSpan.Zero;
        public IReadOnlyList<ReuniaoDesktopPoc> Reunioes { get; }
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
        public DesktopRuntimeInfo Ambiente { get; } = new(
            @"C:\Users\felipe\AppData\Local\Anamnesis\config.json",
            @"C:\Users\felipe\AppData\Local\Anamnesis\anamnesis.db",
            @"C:\Users\felipe\Documents\Anamnesis\Reunioes",
            "Codex",
            [
                new ProntidaoDesktopItem("OBS", true, "Configurado"),
                new ProntidaoDesktopItem("Whisper CLI", false, "Executável local não encontrado")
            ]);
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
        public string? UltimoTextoBusca { get; private set; }
        public FormatoExportacaoAta? UltimoFormatoExportado { get; private set; }
        public string? UltimoDestinoExportado { get; private set; }
        public string? UltimoVaultPublicado { get; private set; }
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

        public Task<IReadOnlyList<ReuniaoDesktopPoc>> BuscarReunioesAsync(
            string? texto,
            string? status,
            DateTimeOffset? criadaDesde,
            CancellationToken cancellationToken)
        {
            UltimoTextoBusca = texto;
            return Task.FromResult<IReadOnlyList<ReuniaoDesktopPoc>>(Reunioes);
        }

        public Task<string> ExportarAtaAsync(
            Guid id,
            FormatoExportacaoAta formato,
            string caminhoDestino,
            bool sobrescrever,
            CancellationToken cancellationToken)
        {
            UltimoFormatoExportado = formato;
            UltimoDestinoExportado = caminhoDestino;
            return Task.FromResult(caminhoDestino);
        }

        public Task<string> PublicarAtaObsidianAsync(
            Guid id,
            string caminhoVault,
            string subpasta,
            CancellationToken cancellationToken)
        {
            UltimoVaultPublicado = caminhoVault;
            return Task.FromResult(Path.Combine(caminhoVault, subpasta, "ata.md"));
        }

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
            Transcricao = ["Pessoa 1: decisão importante.", "Pessoa 2: confirmado."],
            Decisoes =
            [
                "Adotar um reporte mensal das atividades.",
                "Iniciar o acompanhamento em agosto de 2026."
            ],
            Tarefas = ["Felipe: concluir atividade."],
            SecaoCorrespondente = "Transcrição",
            TrechoCorrespondente = "...incidente resolvido com segurança..."
        };
    }

    private sealed class DesktopSessionFluxoManualFake : IDesktopSession
    {
        private readonly List<ReuniaoDesktopPoc> _reunioes = [];
        private readonly Guid _outraReuniaoId = Guid.NewGuid();

        public bool ModoDemonstracao => false;
        public EtapaDesktopPoc Etapa { get; private set; } = EtapaDesktopPoc.Pronto;
        public TimeSpan DuracaoGravacao => TimeSpan.Zero;
        public Guid ReuniaoId { get; } = Guid.NewGuid();
        public string? UltimoTitulo { get; private set; }
        public int Atualizacoes { get; private set; }
        public Guid? ReuniaoAtivaId => Etapa == EtapaDesktopPoc.Gravando ? ReuniaoId : null;
        public Guid? ReuniaoAcompanhadaId => ReuniaoId;
        public IReadOnlyList<ReuniaoDesktopPoc> Reunioes => _reunioes;
        public IReadOnlyList<EventoObservabilidadePoc> EventosOperacionais =>
        [
            new EventoObservabilidadePoc(
                DateTimeOffset.UtcNow.AddSeconds(Atualizacoes),
                NivelEventoPoc.Info,
                "Worker",
                "worker.transcricao",
                "Whisper processando a reunião manual.",
                $"r:{ReuniaoId:N}",
                45),
            new EventoObservabilidadePoc(
                DateTimeOffset.UtcNow.AddSeconds(Atualizacoes),
                NivelEventoPoc.Info,
                "Worker",
                "evento.de.outra.reuniao",
                "Evento que não pode aparecer no console filtrado.",
                $"r:{_outraReuniaoId:N}",
                22)
        ];

        public Task AtualizarAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Atualizacoes++;
            return Task.CompletedTask;
        }

        public Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UltimoTitulo = titulo;
            Etapa = EtapaDesktopPoc.Gravando;
            return Task.CompletedTask;
        }

        public void AvancarGravacao()
        {
        }

        public Task EncerrarGravacaoAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _reunioes.Clear();
            _reunioes.Add(CriarReuniao("Transcrevendo"));
            Etapa = EtapaDesktopPoc.Processando;
            return Task.CompletedTask;
        }

        public void Concluir()
        {
            _reunioes[0].Status = "Ata pronta";
            Etapa = EtapaDesktopPoc.Concluido;
        }

        public void ConcluirProcessamentoSimulado() => Concluir();

        public Task<ReuniaoDesktopPoc?> ObterDetalheAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<ReuniaoDesktopPoc?>(
                reuniaoId == ReuniaoId && _reunioes.Count > 0 ? _reunioes[0] : null);

        public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;

        private ReuniaoDesktopPoc CriarReuniao(string status) => new()
        {
            Id = ReuniaoId,
            Titulo = UltimoTitulo ?? "Reunião sem título",
            Data = "Agora",
            Plataforma = "Captura OBS",
            Duracao = "00:15:00",
            Status = status,
            Resumo = "A reunião manual está sendo processada localmente.",
            PontosPrincipais = [],
            Transcricao = ["00:00:03  Transcrição final da reunião manual."],
            Decisoes = [],
            Tarefas = []
        };
    }
}

[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class InterfaceWindowsGrupo
{
    public const string Nome = "Interface Windows";
}

using System.Text;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Application.UseCases;
using Anamnesis.Infrastructure.Arquivos;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Deteccao;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Obs;
using Anamnesis.Infrastructure.Observabilidade;
using Anamnesis.Infrastructure.Persistencia;
using Anamnesis.Infrastructure.Processos;

namespace Anamnesis.Tray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        try
        {
            return Executar(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Falha do Tray: {exception.Message}");
            var modoSemInterface = args.Any(argumento =>
                string.Equals(argumento, "--diagnostico-deteccao", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argumento, "--gravar-teste-segundos", StringComparison.OrdinalIgnoreCase));
            if (!modoSemInterface)
            {
                ApplicationConfiguration.Initialize();
                MessageBox.Show(
                    $"O Anamnesis nao conseguiu iniciar. {exception.Message}",
                    "Anamnesis",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return 1;
        }
    }

    private static int Executar(IReadOnlyList<string> argumentos)
    {
        if (DesktopPocOptions.EstaAtivo(argumentos))
        {
            ApplicationConfiguration.Initialize();
            DesktopPocTheme.HerdarDoWindows();
            System.Windows.Forms.Application.Run(new DesktopPocForm(DesktopPocTheme.ObterAtual()));
            return 0;
        }

        var modoValidacao = ModoValidacaoTrayOptions.Obter(argumentos);
        var diagnosticoDeteccao = DiagnosticoDeteccaoOptions.Obter(argumentos);
        var encerrarParaAtualizacao = argumentos.Contains(
            "--encerrar-para-atualizacao",
            StringComparer.OrdinalIgnoreCase);
        using var instanciaUnica = modoValidacao is null && diagnosticoDeteccao is null
            ? InstanciaUnicaTray.Criar(
                Environment.GetEnvironmentVariable("ANAMNESIS_TRAY_INSTANCE_KEY"))
            : null;
        if (instanciaUnica is { EhPrimaria: false })
        {
            if (encerrarParaAtualizacao)
            {
                instanciaUnica.SinalizarEncerramentoParaAtualizacao();
            }
            else
            {
                instanciaUnica.SinalizarPrimeiraInstancia();
            }

            return 0;
        }

        if (encerrarParaAtualizacao)
        {
            return 0;
        }

        var caminhoConfiguracao = ObterCaminhoConfiguracao();
        var arquivoConfiguracao = new ArquivoConfiguracao(caminhoConfiguracao);
        var configuracao = arquivoConfiguracao.CarregarAsync(CancellationToken.None).GetAwaiter().GetResult();
        if (diagnosticoDeteccao is not null)
        {
            return ExecutarDiagnosticoDeteccaoAsync(
                    new WindowsSinaisReuniaoSource(configuracao.Deteccao),
                    configuracao.Deteccao.Modo,
                    diagnosticoDeteccao)
                .GetAwaiter()
                .GetResult();
        }

        var workerLauncher = CriarWorkerLauncher(modoValidacao, caminhoConfiguracao);
        var reuniaoRepository = new SqliteReuniaoRepository(configuracao.CaminhoBanco);
        var jobQueue = new SqliteJobQueue(configuracao.CaminhoBanco);
        var jobQuery = new SqliteJobQuery(configuracao.CaminhoBanco);
        var eventoRepository = new SqliteEventoOperacionalRepository(configuracao.CaminhoBanco);
        var journal = new JornalOperacional(eventoRepository, TimeProvider.System);
        journal.RemoverExpiradosAsync(configuracao.RetencaoEventosDias, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var enderecoObs = Uri.TryCreate(configuracao.EnderecoObs, UriKind.Absolute, out var endereco) && endereco is not null
            ? endereco!
            : ObsWebSocketOptions.Padrao.Endereco;
        var controlarGravacao = new ControlarGravacaoHandler(
            reuniaoRepository,
            jobQueue,
            new ObsGravador(new ObsWebSocketOptions(enderecoObs, configuracao.SenhaObs)),
            workerLauncher,
            new ObsProcessPreflight(
                enderecoObs,
                ObsProcessPreflight.ResolverCaminhoExecutavel(configuracao.CaminhoExecutavelObs)),
            TimeProvider.System,
            journal);
        var sessaoDesktop = new DesktopRealSession(
            new SqliteReuniaoQuery(configuracao.CaminhoBanco),
            jobQuery,
            controlarGravacao,
            new WindowsArtefatoLauncher(),
            TimeProvider.System,
            new DesktopRuntimeInfo(
                caminhoConfiguracao,
                configuracao.CaminhoBanco,
                configuracao.DiretorioArquivo,
                configuracao.NomeCli,
                DiagnosticosLocais.Avaliar(configuracao)
                    .Select(item => new ProntidaoDesktopItem(
                        item.Nome,
                        item.Disponivel,
                        item.Mensagem))
                    .ToArray()),
            eventoRepository,
            jobQuery,
            journal);

        if (modoValidacao is not null)
        {
            return ExecutarModoValidacaoAsync(controlarGravacao, modoValidacao)
                .GetAwaiter()
                .GetResult();
        }

        // O detector só pode iniciar depois de o SQLite revelar uma eventual gravação
        // anterior. Essa leitura é local e nunca consulta ou comanda o OBS.
        sessaoDesktop.AtualizarAsync(CancellationToken.None).GetAwaiter().GetResult();

        ApplicationConfiguration.Initialize();
        DesktopPocTheme.HerdarDoWindows();
        using var iconeAplicacao = IconeAnamnesis.Carregar();
        using var icone = new NotifyIcon
        {
            Icon = iconeAplicacao,
            Text = "Anamnesis",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        var detector = new DetectorReuniao(
            new WindowsSinaisReuniaoSource(configuracao.Deteccao),
            new PoliticaDeteccaoReuniao(configuracao.Deteccao.CriarPoliticaOptions()),
            TimeProvider.System,
            journal);
        using var capturaInstantanea = new CapturaInstantaneaController(
            detector,
            sessaoDesktop,
            new DeteccaoPromptForm(
                icone,
                DesktopPocTheme.ObterAtual(),
                DesktopPocSystemPreferences.Obter()));
        var iniciar = new ToolStripMenuItem("Iniciar gravação");
        var encerrar = new ToolStripMenuItem("Encerrar gravação") { Enabled = false };
        var processarPendencias = new ToolStripMenuItem("Processar pendências");
        var estado = new ToolStripMenuItem("Estado: Pronto") { Enabled = false };
        var inicializacaoWindows = new InicializacaoWindows(Environment.ProcessPath!);
        var iniciarComWindows = new ToolStripMenuItem("Iniciar com o Windows")
        {
            Checked = inicializacaoWindows.EstaAtiva,
            CheckOnClick = true
        };
        DesktopPocForm? janela = null;
        var saindo = false;
        var avisoBandejaExibido = false;

        DesktopPocForm CriarJanela()
        {
            var novaJanela = new DesktopPocForm(
                DesktopPocTheme.ObterAtual(),
                DesktopPocSystemPreferences.Obter(),
                sessaoDesktop);
            novaJanela.FormClosing += (_, evento) =>
            {
                if (saindo || evento.CloseReason != CloseReason.UserClosing)
                {
                    return;
                }

                evento.Cancel = true;
                novaJanela.Hide();
                if (!avisoBandejaExibido)
                {
                    avisoBandejaExibido = true;
                    icone.ShowBalloonTip(
                        3000,
                        "Anamnesis continua ativo",
                        "Use o ícone da bandeja para abrir ou sair.",
                        ToolTipIcon.Info);
                }
            };
            novaJanela.FormClosed += (_, _) => janela = null;
            return novaJanela;
        }

        void AbrirJanela()
        {
            janela ??= CriarJanela();

            if (janela.WindowState == FormWindowState.Minimized)
            {
                janela.WindowState = FormWindowState.Normal;
            }

            janela.Show();
            janela.Activate();
        }

        bool TentarEncerrarAplicativo(bool permitirRecuperacaoDeGravacao)
        {
            if (!permitirRecuperacaoDeGravacao &&
                !sessaoDesktop.PodeEncerrarParaAtualizacao)
            {
                AbrirJanela();
                MessageBox.Show(
                    "Há uma gravação ativa ou um comando de gravação em andamento. " +
                    "Finalize-o antes de atualizar o Anamnesis; " +
                    "o instalador não encerra uma gravação à força.",
                    "Atualização do Anamnesis",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            saindo = true;
            icone.Visible = false;
            janela?.Close();
            System.Windows.Forms.Application.Exit();
            return true;
        }

        icone.ContextMenuStrip.Items.Add("Abrir Anamnesis", null, (_, _) => AbrirJanela());
        icone.MouseClick += (_, evento) =>
        {
            if (evento.Button == MouseButtons.Left)
            {
                AbrirJanela();
            }
        };
        icone.DoubleClick += (_, _) => AbrirJanela();

        icone.ContextMenuStrip.Items.Add(estado);
        icone.ContextMenuStrip.Items.Add(new ToolStripMenuItem(
            $"Detecção: {configuracao.Deteccao.Modo}")
        {
            Enabled = false
        });
        icone.ContextMenuStrip.Items.Add("Silenciar detecção por 1 h", null, async (_, _) =>
        {
            await capturaInstantanea.SilenciarAsync(CancellationToken.None);
            icone.ShowBalloonTip(
                3000,
                "Detecção silenciada",
                "O início manual continua disponível.",
                ToolTipIcon.Info);
        });

        icone.ContextMenuStrip.Items.Add("Diagnósticos", null, (_, _) =>
        {
            var texto = string.Join(
                Environment.NewLine,
                DiagnosticosLocais.Avaliar(configuracao).Select(diagnostico =>
                    $"{diagnostico.Nome}: {diagnostico.Mensagem}"));
            MessageBox.Show(texto, "Diagnósticos do Anamnesis", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        icone.ContextMenuStrip.Items.Add("Abrir configurações", null, (_, _) =>
        {
            AbrirJanela();
            janela!.AbrirConfiguracoes();
        });
        iniciarComWindows.Click += (_, _) =>
        {
            try
            {
                if (iniciarComWindows.Checked)
                {
                    inicializacaoWindows.Ativar();
                }
                else
                {
                    inicializacaoWindows.Desativar();
                }
            }
            catch (Exception exception)
            {
                iniciarComWindows.Checked = inicializacaoWindows.EstaAtiva;
                MostrarErro(exception);
            }
        };
        icone.ContextMenuStrip.Items.Add(iniciarComWindows);
        icone.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        icone.ContextMenuStrip.Items.Add(iniciar);
        icone.ContextMenuStrip.Items.Add(encerrar);
        icone.ContextMenuStrip.Items.Add(processarPendencias);
        icone.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        icone.ContextMenuStrip.Items.Add("Sair", null, (_, _) =>
        {
            if (!sessaoDesktop.PodeEncerrarParaAtualizacao &&
                MessageBox.Show(
                    "Há uma gravação ativa ou um comando de gravação em andamento. " +
                    "Deseja sair e deixar a recuperação pendente?",
                    "Sair do Anamnesis",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            TentarEncerrarAplicativo(permitirRecuperacaoDeGravacao: true);
        });

        iniciar.Click += async (_, _) =>
        {
            try
            {
                await sessaoDesktop.IniciarGravacaoAsync("Reunião sem título", CancellationToken.None);
                iniciar.Enabled = false;
                encerrar.Enabled = true;
                icone.ShowBalloonTip(3000, "Anamnesis", "Gravação iniciada.", ToolTipIcon.Info);
            }
            catch (Exception exception)
            {
                MostrarErro(exception);
            }
        };
        encerrar.Click += async (_, _) =>
        {
            try
            {
                await sessaoDesktop.EncerrarGravacaoAsync(CancellationToken.None);
                iniciar.Enabled = true;
                encerrar.Enabled = false;
                icone.ShowBalloonTip(3000, "Anamnesis", "Gravação enviada para processamento.", ToolTipIcon.Info);
            }
            catch (WorkerNaoIniciadoException exception)
            {
                iniciar.Enabled = true;
                encerrar.Enabled = false;
                MessageBox.Show(
                    $"{exception.Message}{Environment.NewLine}O job permanece salvo. Use 'Processar pendências' para tentar novamente.",
                    "Processamento pendente",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception exception)
            {
                MostrarErro(exception);
            }
        };

        processarPendencias.Click += async (_, _) =>
        {
            try
            {
                await workerLauncher.IniciarAsync(CancellationToken.None);
                icone.ShowBalloonTip(3000, "Anamnesis", "Worker iniciado para processar pendências.", ToolTipIcon.Info);
            }
            catch (Exception exception)
            {
                MostrarErro(exception);
            }
        };

        try
        {
            workerLauncher.IniciarAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            icone.ShowBalloonTip(5000, "Anamnesis", $"Worker não iniciado: {exception.Message}", ToolTipIcon.Warning);
        }

        var atualizandoMenu = false;
        using var sincronizarMenu = new System.Windows.Forms.Timer { Interval = 2000 };
        sincronizarMenu.Tick += async (_, _) =>
        {
            if (atualizandoMenu)
            {
                return;
            }

            atualizandoMenu = true;
            try
            {
                var menu = await TrayMenuState.AtualizarAsync(
                    sessaoDesktop,
                    CancellationToken.None);
                estado.Text = $"Estado: {menu.Estado}";
                iniciar.Enabled = menu.PodeIniciar;
                encerrar.Enabled = menu.PodeEncerrar;
                processarPendencias.Text = menu.TextoPendencias;
                icone.Text = sessaoDesktop.RecuperacaoPendente
                    ? "Anamnesis • Recuperação pendente"
                    : sessaoDesktop.Etapa == EtapaDesktopPoc.Gravando
                        ? "Anamnesis • Gravando"
                        : "Anamnesis • Pronto";
            }
            catch (Exception exception)
            {
                await sessaoDesktop.RegistrarFalhaOperacionalAsync(
                    "tray.atualizar_estado",
                    exception,
                    CancellationToken.None);
            }
            finally
            {
                atualizandoMenu = false;
            }
        };
        sincronizarMenu.Start();
        using var detectarReuniao = new System.Windows.Forms.Timer { Interval = 1000 };
        detectarReuniao.Tick += async (_, _) =>
            await capturaInstantanea.ProcessarAsync(CancellationToken.None);
        if (configuracao.Deteccao.Modo != ModoDeteccaoReuniao.Manual)
        {
            detectarReuniao.Start();
        }

        using var despachante = new Control();
        _ = despachante.Handle;
        instanciaUnica!.ObservarAtivacao(() =>
        {
            if (!despachante.IsDisposed && despachante.IsHandleCreated)
            {
                despachante.BeginInvoke((Action)AbrirJanela);
            }
        });
        instanciaUnica.ObservarEncerramentoParaAtualizacao(() =>
        {
            if (!despachante.IsDisposed && despachante.IsHandleCreated)
            {
                despachante.BeginInvoke((Action)(() =>
                {
                    TentarEncerrarAplicativo(permitirRecuperacaoDeGravacao: false);
                }));
            }
        });
        if (!argumentos.Contains("--background", StringComparer.OrdinalIgnoreCase))
        {
            AbrirJanela();
        }

        System.Windows.Forms.Application.Run();
        return 0;
    }

    private static async Task<int> ExecutarModoValidacaoAsync(
        ControlarGravacaoHandler controlarGravacao,
        ModoValidacaoTrayOptions options)
    {
        Console.WriteLine($"Gravação de teste iniciada por {options.Duracao.TotalSeconds:0} segundo(s).");
        var reuniaoId = await controlarGravacao.IniciarAsync("Gravação de teste", CancellationToken.None);
        Console.WriteLine($"ReuniaoId={reuniaoId}");
        await Task.Delay(options.Duracao);
        await controlarGravacao.FinalizarAsync(reuniaoId, CancellationToken.None);
        Console.WriteLine("Gravação de teste concluída e job persistido.");
        return 0;
    }

    private static async Task<int> ExecutarDiagnosticoDeteccaoAsync(
        WindowsSinaisReuniaoSource fonte,
        ModoDeteccaoReuniao modo,
        DiagnosticoDeteccaoOptions options)
    {
        await using var arquivo = CriarArquivoDiagnostico(options.CaminhoSaida);
        return await DiagnosticoDeteccaoRunner.ExecutarAsync(
            fonte,
            modo,
            options,
            arquivo ?? Console.Out,
            CancellationToken.None);
    }

    private static StreamWriter? CriarArquivoDiagnostico(string? caminhoSaida)
    {
        if (string.IsNullOrWhiteSpace(caminhoSaida))
        {
            return null;
        }

        var caminhoCompleto = Path.GetFullPath(caminhoSaida);
        var diretorio = Path.GetDirectoryName(caminhoCompleto);
        if (string.IsNullOrWhiteSpace(diretorio) || !Directory.Exists(diretorio))
        {
            throw new DirectoryNotFoundException(
                "O diretório de saída do diagnóstico não existe.");
        }

        return new StreamWriter(new FileStream(
            caminhoCompleto,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read));
    }

    private static IWorkerLauncher CriarWorkerLauncher(
        ModoValidacaoTrayOptions? modoValidacao,
        string caminhoConfiguracao)
    {
        if (modoValidacao is { IniciarWorker: false })
        {
            return new WorkerLauncherNulo();
        }

        var caminhoWorker = WorkerProcessLauncher.ResolverCaminhoExecutavel(
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("ANAMNESIS_WORKER_EXECUTAVEL"));
        return new WorkerProcessLauncher(caminhoWorker, caminhoConfiguracao);
    }

    private static string ObterCaminhoConfiguracao()
    {
        var caminhoDefinido = Environment.GetEnvironmentVariable("ANAMNESIS_CONFIGURACAO");
        if (!string.IsNullOrWhiteSpace(caminhoDefinido))
        {
            return Path.GetFullPath(caminhoDefinido);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Anamnesis",
            "config.json");
    }

    private static void MostrarErro(Exception exception) =>
        MessageBox.Show(exception.Message, "Anamnesis", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private sealed class WorkerLauncherNulo : IWorkerLauncher
    {
        public Task IniciarAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

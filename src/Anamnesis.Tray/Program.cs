using System.Diagnostics;
using System.Text;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.UseCases;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Obs;
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
            return 1;
        }
    }

    private static int Executar(IReadOnlyList<string> argumentos)
    {
        var modoValidacao = ModoValidacaoTrayOptions.Obter(argumentos);
        var caminhoConfiguracao = ObterCaminhoConfiguracao();
        var arquivoConfiguracao = new ArquivoConfiguracao(caminhoConfiguracao);
        var configuracao = arquivoConfiguracao.CarregarAsync(CancellationToken.None).GetAwaiter().GetResult();
        var workerLauncher = CriarWorkerLauncher(modoValidacao, caminhoConfiguracao);
        var enderecoObs = Uri.TryCreate(configuracao.EnderecoObs, UriKind.Absolute, out var endereco) && endereco is not null
            ? endereco!
            : ObsWebSocketOptions.Padrao.Endereco;
        var controlarGravacao = new ControlarGravacaoHandler(
            new SqliteReuniaoRepository(configuracao.CaminhoBanco),
            new SqliteJobQueue(configuracao.CaminhoBanco),
            new ObsGravador(new ObsWebSocketOptions(enderecoObs, configuracao.SenhaObs)),
            workerLauncher,
            new ObsProcessPreflight(
                enderecoObs,
                ObsProcessPreflight.ResolverCaminhoExecutavel(configuracao.CaminhoExecutavelObs)),
            TimeProvider.System);

        if (modoValidacao is not null)
        {
            return ExecutarModoValidacaoAsync(controlarGravacao, modoValidacao)
                .GetAwaiter()
                .GetResult();
        }

        ApplicationConfiguration.Initialize();
        using var icone = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Anamnesis",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        var iniciar = new ToolStripMenuItem("Iniciar gravação de teste");
        var encerrar = new ToolStripMenuItem("Encerrar gravação de teste") { Enabled = false };
        var processarPendencias = new ToolStripMenuItem("Processar pendências");
        Guid? reuniaoEmGravacao = null;

        icone.ContextMenuStrip.Items.Add("Diagnósticos", null, (_, _) =>
        {
            var texto = string.Join(
                Environment.NewLine,
                DiagnosticosLocais.Avaliar(configuracao).Select(diagnostico =>
                    $"{diagnostico.Nome}: {diagnostico.Mensagem}"));
            MessageBox.Show(texto, "Diagnósticos do Anamnesis", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        icone.ContextMenuStrip.Items.Add("Abrir configuração", null, (_, _) =>
        {
            var inicio = new ProcessStartInfo("notepad.exe") { UseShellExecute = true };
            inicio.ArgumentList.Add(caminhoConfiguracao);
            Process.Start(inicio);
        });
        icone.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        icone.ContextMenuStrip.Items.Add(iniciar);
        icone.ContextMenuStrip.Items.Add(encerrar);
        icone.ContextMenuStrip.Items.Add(processarPendencias);
        icone.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        icone.ContextMenuStrip.Items.Add("Sair", null, (_, _) => System.Windows.Forms.Application.Exit());

        iniciar.Click += async (_, _) =>
        {
            try
            {
                reuniaoEmGravacao = await controlarGravacao.IniciarAsync("Gravação de teste", CancellationToken.None);
                iniciar.Enabled = false;
                encerrar.Enabled = true;
                icone.ShowBalloonTip(3000, "Anamnesis", "Gravação de teste iniciada.", ToolTipIcon.Info);
            }
            catch (Exception exception)
            {
                MostrarErro(exception);
            }
        };
        encerrar.Click += async (_, _) =>
        {
            if (reuniaoEmGravacao is null)
            {
                return;
            }

            try
            {
                await controlarGravacao.FinalizarAsync(reuniaoEmGravacao.Value, CancellationToken.None);
                reuniaoEmGravacao = null;
                iniciar.Enabled = true;
                encerrar.Enabled = false;
                icone.ShowBalloonTip(3000, "Anamnesis", "Gravação enviada para processamento.", ToolTipIcon.Info);
            }
            catch (WorkerNaoIniciadoException exception)
            {
                reuniaoEmGravacao = null;
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

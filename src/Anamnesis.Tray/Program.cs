using System.Diagnostics;
using System.Text;
using Anamnesis.Application.UseCases;
using Anamnesis.Infrastructure.Configuracao;
using Anamnesis.Infrastructure.Fila;
using Anamnesis.Infrastructure.Obs;
using Anamnesis.Infrastructure.Persistencia;

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
        var enderecoObs = Uri.TryCreate(configuracao.EnderecoObs, UriKind.Absolute, out var endereco) && endereco is not null
            ? endereco!
            : ObsWebSocketOptions.Padrao.Endereco;
        var controlarGravacao = new ControlarGravacaoHandler(
            new SqliteReuniaoRepository(configuracao.CaminhoBanco),
            new SqliteJobQueue(configuracao.CaminhoBanco),
            new ObsGravador(new ObsWebSocketOptions(enderecoObs, configuracao.SenhaObs)),
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
            catch (Exception exception)
            {
                MostrarErro(exception);
            }
        };

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
}

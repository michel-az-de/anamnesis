using System.Diagnostics;
using Anamnesis.Application.Contracts;

namespace Anamnesis.Infrastructure.Arquivos;

public sealed class WindowsArtefatoLauncher : IArtefatoLauncher
{
    private static readonly HashSet<string> ExtensoesPermitidas = new(
        [".md", ".txt", ".mp4", ".mkv", ".wav", ".webm", ".m4a", ".mp3"],
        StringComparer.OrdinalIgnoreCase);

    private readonly Action<ProcessStartInfo> _iniciar;

    public WindowsArtefatoLauncher()
        : this(inicio => Process.Start(inicio))
    {
    }

    internal WindowsArtefatoLauncher(Action<ProcessStartInfo> iniciar)
    {
        _iniciar = iniciar ?? throw new ArgumentNullException(nameof(iniciar));
    }

    public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminhoCompleto = ValidarArquivo(caminho);
        _iniciar(new ProcessStartInfo(caminhoCompleto)
        {
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var caminhoCompleto = Path.GetFullPath(caminho);
        if (Directory.Exists(caminhoCompleto))
        {
            var abrirPasta = new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true
            };
            abrirPasta.ArgumentList.Add(caminhoCompleto);
            _iniciar(abrirPasta);
            return Task.CompletedTask;
        }

        caminhoCompleto = ValidarArquivo(caminhoCompleto);
        var mostrarArquivo = new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true
        };
        mostrarArquivo.ArgumentList.Add("/select,");
        mostrarArquivo.ArgumentList.Add(caminhoCompleto);
        _iniciar(mostrarArquivo);
        return Task.CompletedTask;
    }

    private static string ValidarArquivo(string caminho)
    {
        var caminhoCompleto = Path.GetFullPath(caminho);
        if (!File.Exists(caminhoCompleto))
        {
            throw new FileNotFoundException("O artefato não foi encontrado.", caminhoCompleto);
        }

        if (!ExtensoesPermitidas.Contains(Path.GetExtension(caminhoCompleto)))
        {
            throw new InvalidOperationException("O tipo de arquivo não pode ser aberto pelo Anamnesis.");
        }

        return caminhoCompleto;
    }
}

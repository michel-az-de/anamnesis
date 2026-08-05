using System.Runtime.InteropServices;
using Anamnesis.Application.Contracts;

namespace Anamnesis.Infrastructure.Retencao;

public sealed class LixeiraWindows : IGravacaoLixeira
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;

    public Task<bool> ExisteAsync(string caminhoArquivo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(caminhoArquivo));
    }

    public Task MoverAsync(string caminhoArquivo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(caminhoArquivo))
        {
            throw new FileNotFoundException("A gravação não foi encontrada.", caminhoArquivo);
        }

        var operacao = new ShFileOpStruct
        {
            Funcao = FoDelete,
            Origem = $"{caminhoArquivo}\0\0",
            Flags = FofSilent | FofNoConfirmation | FofAllowUndo | FofNoErrorUi
        };
        var resultado = ShFileOperation(ref operacao);
        if (resultado != 0 || operacao.OperacaoAbortada)
        {
            throw new IOException($"Não foi possível mover a gravação para a Lixeira (código {resultado}).");
        }

        return Task.CompletedTask;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int ShFileOperation(ref ShFileOpStruct operacao);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr Janela;
        public uint Funcao;
        public string Origem;
        public string? Destino;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool OperacaoAbortada;
        public IntPtr Mapeamentos;
        public string? TituloProgresso;
    }
}

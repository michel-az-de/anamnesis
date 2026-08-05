using System.Security.Cryptography;
using System.Text;

namespace Anamnesis.Infrastructure.Processos;

/// <summary>
/// Exclusividade de processo do Worker sobre um banco local. É esta garantia que torna correta
/// a liberação de todas as reservas ativas na retomada da fila (ver ADR-012): sem ela, um Worker
/// que sobe rouba o job de outro que ainda está processando.
/// </summary>
public sealed class InstanciaUnicaWorker : IDisposable
{
    private const string Prefixo = @"Local\Anamnesis.Worker.";
    private readonly Mutex _mutex;
    private bool _liberado;

    private InstanciaUnicaWorker(Mutex mutex) => _mutex = mutex;

    /// <summary>
    /// O nome deriva do banco, e não é constante, para que dois usuários da mesma máquina
    /// tenham Workers independentes e para que bancos temporários de teste nunca colidam
    /// entre si nem com o Worker real da máquina do desenvolvedor.
    /// </summary>
    public static string ObterNome(string caminhoBanco)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoBanco);
        var normalizado = Path.GetFullPath(caminhoBanco).ToLowerInvariant();
        var resumo = SHA256.HashData(Encoding.UTF8.GetBytes(normalizado));
        return Prefixo + Convert.ToHexStringLower(resumo)[..32];
    }

    public static InstanciaUnicaWorker? TentarAdquirir(string caminhoBanco)
    {
        var mutex = new Mutex(initiallyOwned: false, ObterNome(caminhoBanco));
        try
        {
            if (Adquirir(mutex))
            {
                return new InstanciaUnicaWorker(mutex);
            }
        }
        catch
        {
            mutex.Dispose();
            throw;
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (!_liberado)
        {
            _liberado = true;
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }

    private static bool Adquirir(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // O Worker anterior morreu sem liberar. A posse já foi transferida para este
            // processo, que é justamente quem deve retomar a fila.
            return true;
        }
    }
}

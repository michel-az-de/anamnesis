using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Anamnesis.Infrastructure.Configuracao;

/// <summary>
/// Protege segredos da configuração local com DPAPI, amarrados ao usuário do Windows.
/// O prefixo distingue um valor protegido de um valor legado em texto claro, para que
/// configurações existentes continuem carregando e sejam migradas no próximo salvamento.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class SegredoLocal
{
    private const string Prefixo = "dpapi:";

    public static string? Proteger(string? valor)
    {
        if (string.IsNullOrEmpty(valor) || EstaProtegido(valor))
        {
            return valor;
        }

        var protegido = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(valor),
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);
        return Prefixo + Convert.ToBase64String(protegido);
    }

    public static string? Revelar(string? valor)
    {
        if (string.IsNullOrEmpty(valor) || !EstaProtegido(valor))
        {
            return valor;
        }

        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(valor[Prefixo.Length..]),
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception excecao) when (excecao is CryptographicException or FormatException)
        {
            throw new InvalidDataException(
                "A senha do OBS não pôde ser lida: ela foi protegida por outro usuário ou em outra máquina. " +
                "Apague o campo SenhaObs no arquivo de configuração e informe a senha novamente.",
                excecao);
        }
    }

    private static bool EstaProtegido(string valor) =>
        valor.StartsWith(Prefixo, StringComparison.Ordinal);
}

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Media.Audio;
using Windows.Win32.System.Com;

namespace Anamnesis.Infrastructure.Deteccao;

[SupportedOSPlatform("windows6.1")]
internal sealed class WindowsSinaisProbe : IWindowsSinaisProbe
{
    public IReadOnlyCollection<string> ListarFamiliasComCaptura() =>
        ListarFamiliasComSessaoAtiva(EDataFlow.eCapture);

    public IReadOnlyCollection<string> ListarFamiliasComRenderizacao() =>
        ListarFamiliasComSessaoAtiva(EDataFlow.eRender);

    public IReadOnlyCollection<JanelaWindows> ListarJanelas()
    {
        var janelas = new List<JanelaWindows>();
        Windows.Win32.UI.WindowsAndMessaging.WNDENUMPROC callback = (janela, _) =>
        {
            try
            {
                if (!PInvoke.IsWindowVisible(janela) || EstaOcultaPeloDwm(janela))
                {
                    return true;
                }

                var comprimento = Math.Min(PInvoke.GetWindowTextLength(janela), 512);
                if (comprimento <= 0)
                {
                    return true;
                }

                var buffer = new char[comprimento + 1];
                var copiados = PInvoke.GetWindowText(janela, buffer);
                if (copiados <= 0)
                {
                    return true;
                }

                PInvoke.GetWindowThreadProcessId(janela, out var processoId);
                var familia = ObterFamiliaProcesso(processoId);
                if (familia is not null)
                {
                    janelas.Add(new JanelaWindows(
                        familia,
                        new string(buffer, 0, copiados)));
                }
            }
            catch (Exception)
            {
                // A janela pode desaparecer entre a enumeracao, a leitura do titulo e o PID.
            }

            return true;
        };

        bool enumeracaoConcluida = PInvoke.EnumWindows(callback, default);
        var codigoErro = enumeracaoConcluida ? 0 : Marshal.GetLastWin32Error();
        GC.KeepAlive(callback);
        ValidarResultadoEnumeracao(enumeracaoConcluida, codigoErro);
        return janelas;
    }

    internal static void ValidarResultadoEnumeracao(
        bool enumeracaoConcluida,
        int codigoErro)
    {
        if (!enumeracaoConcluida)
        {
            throw new Win32Exception(
                codigoErro,
                "Falha ao enumerar as janelas superiores do Windows.");
        }
    }

    private static IReadOnlyCollection<string> ListarFamiliasComSessaoAtiva(EDataFlow fluxo)
    {
        var familias = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var leituraCompleta = true;
        IMMDeviceEnumerator? enumerador = null;
        IMMDeviceCollection? dispositivos = null;
        try
        {
            enumerador = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            enumerador.EnumAudioEndpoints(
                fluxo,
                DEVICE_STATE.DEVICE_STATE_ACTIVE,
                out dispositivos);
            dispositivos.GetCount(out var quantidade);
            for (uint indice = 0; indice < quantidade; indice++)
            {
                IMMDevice? dispositivo = null;
                try
                {
                    dispositivos.Item(indice, out dispositivo);
                    if (!ListarSessoesAtivas(dispositivo, familias))
                    {
                        leituraCompleta = false;
                    }
                }
                catch (COMException)
                {
                    // Dispositivo removido ou invalidado durante o ciclo apenas reduz confianca.
                    leituraCompleta = false;
                }
                finally
                {
                    LiberarCom(dispositivo);
                }
            }
        }
        finally
        {
            LiberarCom(dispositivos);
            LiberarCom(enumerador);
        }

        return ConcluirLeitura(familias, leituraCompleta);
    }

    internal static IReadOnlyCollection<T> ConcluirLeitura<T>(
        IReadOnlyCollection<T> resultados,
        bool leituraCompleta)
    {
        if (!leituraCompleta)
        {
            throw new WindowsSinaisLeituraParcialException<T>(resultados);
        }

        return resultados;
    }

    private static bool ListarSessoesAtivas(
        IMMDevice dispositivo,
        HashSet<string> familias)
    {
        var leituraCompleta = true;
        object? gerenciadorObjeto = null;
        IAudioSessionEnumerator? sessoes = null;
        try
        {
            var interfaceId = typeof(IAudioSessionManager2).GUID;
            dispositivo.Activate(
                in interfaceId,
                CLSCTX.CLSCTX_ALL,
                null,
                out gerenciadorObjeto);
            var gerenciador = (IAudioSessionManager2)gerenciadorObjeto;
            sessoes = gerenciador.GetSessionEnumerator();
            sessoes.GetCount(out var quantidade);
            for (var indice = 0; indice < quantidade; indice++)
            {
                IAudioSessionControl? sessao = null;
                try
                {
                    sessoes.GetSession(indice, out sessao);
                    sessao.GetState(out var estado);
                    if (estado != AudioSessionState.AudioSessionStateActive ||
                        sessao is not IAudioSessionControl2 sessaoComProcesso)
                    {
                        continue;
                    }

                    sessaoComProcesso.GetProcessId(out var processoId);
                    var familia = ObterFamiliaProcesso(processoId);
                    if (familia is not null)
                    {
                        familias.Add(familia);
                    }
                }
                catch (COMException)
                {
                    // A sessao pode expirar enquanto a colecao e percorrida.
                    leituraCompleta = false;
                }
                finally
                {
                    LiberarCom(sessao);
                }
            }
        }
        finally
        {
            LiberarCom(sessoes);
            LiberarCom(gerenciadorObjeto);
        }

        return leituraCompleta;
    }

    private static string? ObterFamiliaProcesso(uint processoId)
    {
        if (processoId == 0 || processoId > int.MaxValue)
        {
            return null;
        }

        try
        {
            using var processo = Process.GetProcessById((int)processoId);
            return processo.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static bool EstaOcultaPeloDwm(Windows.Win32.Foundation.HWND janela)
    {
        Span<byte> valor = stackalloc byte[sizeof(uint)];
        var resultado = PInvoke.DwmGetWindowAttribute(
            janela,
            DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
            valor);
        return resultado.Succeeded && BitConverter.ToUInt32(valor) != 0;
    }

    private static void LiberarCom(object? instancia)
    {
        if (instancia is not null && Marshal.IsComObject(instancia))
        {
            Marshal.FinalReleaseComObject(instancia);
        }
    }
}

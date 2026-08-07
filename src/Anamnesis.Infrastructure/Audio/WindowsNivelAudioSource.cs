using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Windows.Win32.Media.Audio;
using Windows.Win32.Media.Audio.Endpoints;
using Windows.Win32.System.Com;

namespace Anamnesis.Infrastructure.Audio;

[SupportedOSPlatform("windows6.1")]
public sealed class WindowsNivelAudioSource : INivelAudioSource
{
    public Task<NivelAudioLeitura> LerAsync(CancellationToken cancellationToken) =>
        Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sistema = TentarLer(EDataFlow.eRender);
                var microfone = TentarLer(EDataFlow.eCapture);
                return sistema is null && microfone is null
                    ? NivelAudioLeitura.SemLeitura("Core Audio não forneceu níveis dos dispositivos padrão.")
                    : new NivelAudioLeitura(sistema, microfone);
            },
            cancellationToken);

    internal static int? NormalizarPico(float pico)
    {
        if (float.IsNaN(pico) || float.IsInfinity(pico))
        {
            return null;
        }

        return (int)Math.Round(Math.Clamp(pico, 0F, 1F) * 100F, MidpointRounding.AwayFromZero);
    }

    private static unsafe int? TentarLer(EDataFlow fluxo)
    {
        IMMDeviceEnumerator? enumerador = null;
        IMMDevice? dispositivo = null;
        object? medidorObjeto = null;
        try
        {
            enumerador = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            enumerador.GetDefaultAudioEndpoint(fluxo, ERole.eMultimedia, out dispositivo);
            var interfaceId = typeof(IAudioMeterInformation).GUID;
            dispositivo.Activate(
                &interfaceId,
                CLSCTX.CLSCTX_ALL,
                null,
                out medidorObjeto);
            if (medidorObjeto is not IAudioMeterInformation medidor)
            {
                return null;
            }

            medidor.GetPeakValue(out var pico);
            return NormalizarPico(pico);
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        finally
        {
            LiberarCom(medidorObjeto);
            LiberarCom(dispositivo);
            LiberarCom(enumerador);
        }
    }

    private static void LiberarCom(object? instancia)
    {
        if (instancia is not null && Marshal.IsComObject(instancia))
        {
            Marshal.FinalReleaseComObject(instancia);
        }
    }
}

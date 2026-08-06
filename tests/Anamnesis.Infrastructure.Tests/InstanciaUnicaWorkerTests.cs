using Anamnesis.Infrastructure.Processos;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class InstanciaUnicaWorkerTests
{
    [Fact]
    public void DeveAdquirirExclusividadeParaUmBanco()
    {
        using var instancia = InstanciaUnicaWorker.TentarAdquirir(CriarCaminhoBanco());

        Assert.NotNull(instancia);
    }

    [Fact]
    public void NaoDeveAdquirirEnquantoOutraInstanciaEstiverAtiva()
    {
        var caminhoBanco = CriarCaminhoBanco();
        using var primeira = InstanciaUnicaWorker.TentarAdquirir(caminhoBanco);

        // O mutex é reentrante para a thread que o detém, então quem representa o outro
        // processo aqui é outra thread. Precisa ser uma thread dedicada, e não o pool:
        // esperar por uma task do pool pode executá-la em linha na própria thread do teste.
        InstanciaUnicaWorker? segunda = null;
        var outroProcesso = new Thread(() => segunda = InstanciaUnicaWorker.TentarAdquirir(caminhoBanco));
        outroProcesso.Start();
        outroProcesso.Join();

        Assert.NotNull(primeira);
        Assert.Null(segunda);
    }

    [Fact]
    public void NaoDeveAguardarIndefinidamentePelaExclusividade()
    {
        var caminhoBanco = CriarCaminhoBanco();
        using var primeira = InstanciaUnicaWorker.TentarAdquirir(caminhoBanco)!;
        InstanciaUnicaWorker? segunda = null;
        Exception? falha = null;
        var outroProcesso = new Thread(() =>
        {
            try
            {
                segunda = InstanciaUnicaWorker.AdquirirAguardando(
                    caminhoBanco,
                    TimeSpan.FromMilliseconds(250));
            }
            catch (Exception exception)
            {
                falha = exception;
            }
        });

        outroProcesso.Start();
        var concluiu = outroProcesso.Join(TimeSpan.FromSeconds(5));

        Assert.True(concluiu, "A espera pela exclusividade nao respeitou o teto.");
        Assert.Null(falha);
        Assert.Null(segunda);
    }

    [Fact]
    public void NaoDeveAceitarLimiteInfinitoOuInvalido()
    {
        var caminhoBanco = CriarCaminhoBanco();
        TimeSpan[] limitesInvalidos =
        [
            Timeout.InfiniteTimeSpan,
            TimeSpan.FromMilliseconds(-2),
            TimeSpan.FromMilliseconds((double)int.MaxValue + 1)
        ];

        Assert.All(limitesInvalidos, limite =>
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                InstanciaUnicaWorker.AdquirirAguardando(caminhoBanco, limite)));
    }

    [Fact]
    public void DeveTransferirExclusividadeDentroDoLimite()
    {
        var caminhoBanco = CriarCaminhoBanco();
        var primeira = InstanciaUnicaWorker.TentarAdquirir(caminhoBanco)!;
        using var adquiriu = new ManualResetEventSlim();
        using var liberarSegunda = new ManualResetEventSlim();
        Exception? falha = null;
        var aguardouOutraInstancia = false;
        var outroProcesso = new Thread(() =>
        {
            try
            {
                using var segunda = InstanciaUnicaWorker.AdquirirAguardando(
                    caminhoBanco,
                    TimeSpan.FromSeconds(30))!;
                aguardouOutraInstancia = segunda.AguardouOutraInstancia;
                adquiriu.Set();
                liberarSegunda.Wait();
            }
            catch (Exception exception)
            {
                falha = exception;
                adquiriu.Set();
            }
        });

        outroProcesso.Start();
        var primeiraLiberada = false;
        try
        {
            Assert.True(SpinWait.SpinUntil(
                () => (outroProcesso.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(2)),
                "A segunda thread nao entrou na espera do mutex.");
            Assert.False(adquiriu.IsSet);
            primeira.Dispose();
            primeiraLiberada = true;
            Assert.True(adquiriu.Wait(TimeSpan.FromSeconds(2)));
            Assert.True(aguardouOutraInstancia);
            Assert.Null(falha);
        }
        finally
        {
            if (!primeiraLiberada)
            {
                primeira.Dispose();
            }

            liberarSegunda.Set();
            outroProcesso.Join();
        }
    }

    [Fact]
    public void DeveAdquirirMutexAbandonadoDentroDoLimite()
    {
        var caminhoBanco = CriarCaminhoBanco();
        InstanciaUnicaWorker? abandonada = null;
        var donoAnterior = new Thread(() =>
            abandonada = InstanciaUnicaWorker.TentarAdquirir(caminhoBanco));
        donoAnterior.Start();
        donoAnterior.Join();

        using var recuperada = InstanciaUnicaWorker.AdquirirAguardando(
            caminhoBanco,
            TimeSpan.FromSeconds(2));

        Assert.NotNull(abandonada);
        Assert.NotNull(recuperada);
        GC.KeepAlive(abandonada);
    }

    [Fact]
    public void DevePermitirNovaAquisicaoDepoisDeLiberar()
    {
        var caminhoBanco = CriarCaminhoBanco();
        InstanciaUnicaWorker.TentarAdquirir(caminhoBanco)!.Dispose();

        using var seguinte = InstanciaUnicaWorker.TentarAdquirir(caminhoBanco);

        Assert.NotNull(seguinte);
    }

    [Fact]
    public void DevePermitirInstanciasSimultaneasParaBancosDiferentes()
    {
        using var primeira = InstanciaUnicaWorker.TentarAdquirir(CriarCaminhoBanco());

        using var segunda = InstanciaUnicaWorker.TentarAdquirir(CriarCaminhoBanco());

        Assert.NotNull(primeira);
        Assert.NotNull(segunda);
    }

    [Fact]
    public void DeveDerivarONomeDoCaminhoNormalizadoDoBanco()
    {
        var nome = InstanciaUnicaWorker.ObterNome(@"C:\dados\anamnesis.db");

        Assert.Equal(InstanciaUnicaWorker.ObterNome(@"C:\dados\sub\..\ANAMNESIS.DB"), nome);
        Assert.NotEqual(InstanciaUnicaWorker.ObterNome(@"C:\dados\outro.db"), nome);
        Assert.StartsWith(@"Local\Anamnesis.Worker.", nome, StringComparison.Ordinal);
        Assert.DoesNotContain('\\', nome[@"Local\".Length..]);
    }

    private static string CriarCaminhoBanco() =>
        Path.Combine(Path.GetTempPath(), $"anamnesis-instancia-{Guid.NewGuid():N}.db");
}

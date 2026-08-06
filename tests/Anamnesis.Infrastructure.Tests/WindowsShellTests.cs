using System.Buffers.Binary;
using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WindowsShellTests
{
    [Fact]
    public void InstanciaUnicaDeveSerReservadaAntesDaConfiguracaoLocal()
    {
        var programa = File.ReadAllText(Path.Combine(
            EncontrarRaizRepositorio(),
            "src",
            "Anamnesis.Tray",
            "Program.cs"));

        var reserva = programa.IndexOf("InstanciaUnicaTray.Criar", StringComparison.Ordinal);
        var configuracao = programa.IndexOf("new ArquivoConfiguracao", StringComparison.Ordinal);

        Assert.True(reserva >= 0);
        Assert.True(configuracao >= 0);
        Assert.True(reserva < configuracao);
    }

    [Fact]
    public void DeveManterUmaUnicaInstanciaESinalizarJanelaExistente()
    {
        var chave = $"Anamnesis.Tests.{Guid.NewGuid():N}";
        using var ativacao = new ManualResetEventSlim();
        using var primaria = InstanciaUnicaTray.Criar(chave);
        primaria.ObservarAtivacao(() => ativacao.Set());

        using var secundaria = InstanciaUnicaTray.Criar(chave);

        Assert.True(primaria.EhPrimaria);
        Assert.False(secundaria.EhPrimaria);
        secundaria.SinalizarPrimeiraInstancia();
        Assert.True(ativacao.Wait(TimeSpan.FromSeconds(2)));
    }

    [Theory]
    [InlineData(EtapaDesktopPoc.Pronto, false, 0, "Pronto", true, false, "Processar pendências")]
    [InlineData(EtapaDesktopPoc.Gravando, false, 0, "Gravando", false, true, "Processar pendências")]
    [InlineData(EtapaDesktopPoc.Processando, false, 3, "Processando localmente", true, false, "Processar pendências (3)")]
    [InlineData(EtapaDesktopPoc.Gravando, true, 1, "Recuperação pendente", false, true, "Processar pendências (1)")]
    public void MenuDeveRefletirEstadoReal(
        EtapaDesktopPoc etapa,
        bool recuperacao,
        int pendencias,
        string estado,
        bool podeIniciar,
        bool podeEncerrar,
        string textoPendencias)
    {
        var menu = TrayMenuState.Criar(etapa, recuperacao, pendencias);

        Assert.Equal(estado, menu.Estado);
        Assert.Equal(podeIniciar, menu.PodeIniciar);
        Assert.Equal(podeEncerrar, menu.PodeEncerrar);
        Assert.Equal(textoPendencias, menu.TextoPendencias);
    }

    [Fact]
    public async Task MenuDeveAtualizarSessaoAntesDeRefletirEstado()
    {
        var sessao = new DesktopSessionTrayFake();

        var menu = await TrayMenuState.AtualizarAsync(sessao, CancellationToken.None);

        Assert.Equal(1, sessao.Atualizacoes);
        Assert.Equal("Processando localmente", menu.Estado);
        Assert.Equal("Processar pendências (2)", menu.TextoPendencias);
    }

    [Fact]
    public void InicializacaoComWindowsDeveUsarExecutavelAtualEmSegundoPlano()
    {
        string? comando = null;
        var inicializacao = new InicializacaoWindows(
            @"C:\Program Files\Anamnesis\Anamnesis.Tray.exe",
            () => comando,
            valor => comando = valor);

        inicializacao.Ativar();

        Assert.True(inicializacao.EstaAtiva);
        Assert.Equal(
            "\"C:\\Program Files\\Anamnesis\\Anamnesis.Tray.exe\" --background",
            comando);

        inicializacao.Desativar();
        Assert.False(inicializacao.EstaAtiva);
        Assert.Null(comando);
    }

    [Fact]
    public void IconeDeveConterTamanhosAdequadosParaTrayEWindows()
    {
        var bytes = IconeAnamnesis.ObterBytes();
        Assert.True(bytes.Length > 100);
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        var quantidade = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        Assert.True(quantidade >= 5);

        var tamanhos = Enumerable.Range(0, quantidade)
            .Select(indice =>
            {
                var valor = bytes[6 + (indice * 16)];
                return valor == 0 ? 256 : valor;
            })
            .ToArray();

        Assert.Contains(16, tamanhos);
        Assert.Contains(24, tamanhos);
        Assert.Contains(32, tamanhos);
        Assert.Contains(48, tamanhos);
        Assert.Contains(256, tamanhos);
    }

    private static string EncontrarRaizRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "Anamnesis.sln")))
        {
            atual = atual.Parent;
        }

        return atual?.FullName
            ?? throw new DirectoryNotFoundException("A raiz do repositorio nao foi encontrada.");
    }

    private sealed class DesktopSessionTrayFake : IDesktopSession
    {
        public bool ModoDemonstracao => false;
        public EtapaDesktopPoc Etapa { get; private set; } = EtapaDesktopPoc.Pronto;
        public TimeSpan DuracaoGravacao => TimeSpan.Zero;
        public IReadOnlyList<ReuniaoDesktopPoc> Reunioes => [];
        public int JobsNaFila { get; private set; }
        public int Atualizacoes { get; private set; }

        public Task AtualizarAsync(CancellationToken cancellationToken)
        {
            Atualizacoes++;
            Etapa = EtapaDesktopPoc.Processando;
            JobsNaFila = 2;
            return Task.CompletedTask;
        }

        public Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken) => Task.CompletedTask;
        public void AvancarGravacao() { }
        public Task EncerrarGravacaoAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void ConcluirProcessamentoSimulado() { }
        public Task<ReuniaoDesktopPoc?> ObterDetalheAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<ReuniaoDesktopPoc?>(null);
        public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

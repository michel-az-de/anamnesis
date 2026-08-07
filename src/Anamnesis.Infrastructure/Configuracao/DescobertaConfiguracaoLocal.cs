namespace Anamnesis.Infrastructure.Configuracao;

public sealed class DescobertaConfiguracaoLocal
{
    private const string ImagemWhisper =
        "ghcr.io/ggml-org/whisper.cpp@sha256:de6861ca4d509482d7e7c31e4480ee888a76a48903dad1f448faafa6c915d53c";
    private readonly string _programFiles;
    private readonly string _localAppData;
    private readonly string _diretorioAplicacao;
    private readonly string _path;

    public DescobertaConfiguracaoLocal()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            ObterDiretorioDados(),
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
    {
    }

    internal DescobertaConfiguracaoLocal(
        string programFiles,
        string localAppData,
        string diretorioAplicacao,
        string path)
    {
        _programFiles = Path.GetFullPath(programFiles);
        _localAppData = Path.GetFullPath(localAppData);
        _diretorioAplicacao = Path.GetFullPath(diretorioAplicacao);
        _path = path;
    }

    public ConfiguracaoAnamnesis Criar()
    {
        var obs = EncontrarArquivo(Path.Combine(
            _programFiles,
            "obs-studio",
            "bin",
            "64bit",
            "obs64.exe"));
        var docker = EncontrarArquivo(Path.Combine(
                _programFiles,
                "Docker",
                "Docker",
                "resources",
                "bin",
                "docker.exe"))
            ?? EncontrarNoPath("docker.exe");
        var dockerDesktop = EncontrarArquivo(Path.Combine(
            _programFiles,
            "Docker",
            "Docker",
            "Docker Desktop.exe"));
        var ffmpeg = EncontrarNoPath("ffmpeg.exe") ?? EncontrarFfmpegWinGet();
        var modelo = EncontrarArquivo(Path.Combine(_localAppData, "models", "ggml-base.bin"));
        var schema = EncontrarArquivo(Path.Combine(_diretorioAplicacao, "ata.schema.json"));
        var codex = EncontrarNoPath("codex.exe") ?? EncontrarCodexGerenciado();

        return ConfiguracaoAnamnesis.CriarPadrao() with
        {
            CaminhoBanco = Path.Combine(_localAppData, "anamnesis.db"),
            DiretorioArquivo = Path.Combine(_localAppData, "arquivo"),
            CaminhoExecutavelObs = obs,
            CaminhoExecutavelFfmpeg = ffmpeg ?? string.Empty,
            CaminhoExecutavelWhisper = docker ?? string.Empty,
            CaminhoModeloWhisper = modelo ?? string.Empty,
            ImagemDockerWhisper = docker is not null && modelo is not null ? ImagemWhisper : null,
            CaminhoExecutavelDockerDesktop = dockerDesktop,
            NomeCli = codex is not null && schema is not null ? "Codex CLI" : "CLI configurada",
            CaminhoExecutavelCli = codex is not null && schema is not null ? codex : string.Empty,
            ArgumentoArquivoSaidaCli = codex is not null && schema is not null
                ? "--output-last-message"
                : null,
            ArgumentosCli = codex is not null && schema is not null
                ?
                [
                    "exec",
                    "--skip-git-repo-check",
                    "--sandbox",
                    "read-only",
                    "--output-schema",
                    schema,
                    "-"
                ]
                : []
        };
    }

    private string? EncontrarNoPath(string nomeArquivo)
    {
        foreach (var diretorio in _path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidato = EncontrarArquivo(Path.Combine(diretorio.Trim('"'), nomeArquivo));
            if (candidato is not null)
            {
                return candidato;
            }
        }

        return null;
    }

    private string? EncontrarFfmpegWinGet()
    {
        var raiz = Path.Combine(
            Path.GetDirectoryName(_localAppData) ?? _localAppData,
            "Microsoft",
            "WinGet",
            "Packages");
        return EncontrarRecursivamente(raiz, "ffmpeg.exe", "Gyan.FFmpeg*");
    }

    private string? EncontrarCodexGerenciado() =>
        EncontrarRecursivamente(Path.Combine(_localAppData, "tools", "codex"), "codex.exe", "*");

    private static string ObterDiretorioDados()
    {
        var definido = Environment.GetEnvironmentVariable("ANAMNESIS_DIRETORIO_DADOS");
        return string.IsNullOrWhiteSpace(definido)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Anamnesis")
            : Path.GetFullPath(definido);
    }

    private static string? EncontrarRecursivamente(
        string raiz,
        string nomeArquivo,
        string filtroDiretorio)
    {
        if (!Directory.Exists(raiz))
        {
            return null;
        }

        try
        {
            foreach (var diretorio in Directory.EnumerateDirectories(raiz, filtroDiretorio))
            {
                var arquivo = Directory.EnumerateFiles(
                        diretorio,
                        nomeArquivo,
                        SearchOption.AllDirectories)
                    .OrderByDescending(caminho => caminho, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (arquivo is not null)
                {
                    return Path.GetFullPath(arquivo);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }

        return null;
    }

    private static string? EncontrarArquivo(string caminho) =>
        File.Exists(caminho) ? Path.GetFullPath(caminho) : null;
}

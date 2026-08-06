using Anamnesis.Infrastructure.Persistencia;

return await ExecutarAsync(args);

static async Task<int> ExecutarAsync(string[] argumentos)
{
    try
    {
        return argumentos.FirstOrDefault() switch
        {
            "dono" => await ExecutarDonoAsync(argumentos),
            "concorrente" => await ExecutarConcorrenteAsync(argumentos),
            _ => 2
        };
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.GetType().Name);
        return 1;
    }
}

static async Task<int> ExecutarDonoAsync(string[] argumentos)
{
    if (argumentos.Length != 4)
    {
        return 2;
    }

    using var adquiriu = EventWaitHandle.OpenExisting(argumentos[2]);
    using var liberar = EventWaitHandle.OpenExisting(argumentos[3]);
    var banco = new BancoLocal(
        argumentos[1],
        (conexao, cancellationToken) =>
        {
            adquiriu.Set();
            if (!liberar.WaitOne(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("O teste nao liberou o dono da trava.");
            }

            return Task.CompletedTask;
        },
        limiteExclusividadePreparacao: TimeSpan.FromSeconds(5));
    await using var conexao = await banco.AbrirAsync(CancellationToken.None);
    Console.WriteLine("exclusividade-liberada");
    return 0;
}

static async Task<int> ExecutarConcorrenteAsync(string[] argumentos)
{
    if (argumentos.Length != 3)
    {
        return 2;
    }

    using var detectouContencao = EventWaitHandle.OpenExisting(argumentos[2]);
    var banco = new BancoLocal(
        argumentos[1],
        static (conexao, cancellationToken) => Task.CompletedTask,
        limiteExclusividadePreparacao: TimeSpan.FromSeconds(5),
        aoDetectarContencaoPreparacao: () => detectouContencao.Set());
    await using var conexao = await banco.AbrirAsync(CancellationToken.None);
    Console.WriteLine("exclusividade-transferida");
    return 0;
}

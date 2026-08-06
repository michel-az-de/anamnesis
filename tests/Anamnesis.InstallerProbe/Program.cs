using System.Text;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Persistencia;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

if (args.Length != 3 || !Guid.TryParse(args[2], out var reuniaoId))
{
    Console.Error.WriteLine("Uso: Anamnesis.InstallerProbe <seed|verify> <banco.db> <reuniao-id>.");
    return 2;
}

var comando = args[0];
var caminhoBanco = Path.GetFullPath(args[1]);
var repository = new SqliteReuniaoRepository(caminhoBanco);

if (string.Equals(comando, "seed", StringComparison.OrdinalIgnoreCase))
{
    var reuniao = new Reuniao(
        reuniaoId,
        "Reunião sentinela do instalador",
        new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero));
    await repository.SalvarAsync(reuniao, CancellationToken.None);
    Console.WriteLine($"Reunião sentinela criada: {reuniaoId:N}");
    return 0;
}

if (string.Equals(comando, "verify", StringComparison.OrdinalIgnoreCase))
{
    var reuniao = await repository.ObterAsync(reuniaoId, CancellationToken.None);
    if (reuniao is null ||
        reuniao.Status != StatusReuniao.Agendada ||
        !string.Equals(
            reuniao.Titulo,
            "Reunião sentinela do instalador",
            StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"Reunião sentinela ausente ou alterada: {reuniaoId:N}");
        return 1;
    }

    Console.WriteLine($"Reunião sentinela preservada: {reuniaoId:N}");
    return 0;
}

Console.Error.WriteLine($"Comando desconhecido: {comando}");
return 2;

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

[assembly: InternalsVisibleTo("Anamnesis.Infrastructure.Tests")]
[assembly: InternalsVisibleTo("Anamnesis.JournalProbe")]

// A camada já depende de shell32, do explorer.exe, do OBS e do Docker Desktop: declarar a
// plataforma torna explícito o que sempre foi verdade e dispensa anotar cada ponto de uso.
[assembly: SupportedOSPlatform("windows")]

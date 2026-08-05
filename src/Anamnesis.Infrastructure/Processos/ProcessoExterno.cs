using System.ComponentModel;
using System.Diagnostics;

namespace Anamnesis.Infrastructure.Processos;

internal static class ProcessoExterno
{
    private static readonly TimeSpan LimiteEncerramento = TimeSpan.FromSeconds(2);

    public static CancellationTokenSource CriarDeadline(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "O deadline deve ser maior que zero.");
        }

        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        return deadline;
    }

    public static async Task EncerrarAsync(Process processo)
    {
        try
        {
            if (!processo.HasExited)
            {
                processo.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }
        catch (Win32Exception)
        {
            return;
        }
        catch (NotSupportedException)
        {
            return;
        }

        try
        {
            await processo.WaitForExitAsync(CancellationToken.None)
                .WaitAsync(LimiteEncerramento);
        }
        catch (InvalidOperationException)
        {
        }
        catch (TimeoutException)
        {
        }
    }

    public static async Task ObservarSemSubstituirErroAsync(params Task[] tarefas)
    {
        try
        {
            await Task.WhenAll(tarefas).WaitAsync(LimiteEncerramento);
        }
        catch (Exception) when (tarefas.All(static tarefa => tarefa.IsCompleted))
        {
            // O erro relevante ja e o timeout/cancelamento do processo. A leitura dos pipes
            // e observada apenas para nao gerar excecao nao observada nem trocar a causa real.
        }
        catch (TimeoutException)
        {
        }
    }

    public static TimeoutException CriarTimeout(string nome, TimeSpan timeout, Exception causa) =>
        new($"{nome} excedeu o tempo limite de {Formatar(timeout)}.", causa);

    private static string Formatar(TimeSpan timeout) => timeout.TotalMinutes >= 1
        ? $"{timeout.TotalMinutes:0.##} minuto(s)"
        : $"{timeout.TotalSeconds:0.##} segundo(s)";
}

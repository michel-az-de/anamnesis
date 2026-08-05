namespace Anamnesis.Infrastructure.Whisper;

internal static class ArquivoTemporario
{
    /// <summary>
    /// Exclui sem deixar a limpeza mascarar o erro que a provocou: um arquivo temporário ainda
    /// bloqueado não pode substituir a mensagem real da falha de transcrição.
    /// </summary>
    public static void ExcluirSilenciosamente(string caminho)
    {
        try
        {
            File.Delete(caminho);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

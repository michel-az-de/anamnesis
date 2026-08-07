namespace Anamnesis.Application.UseCases;

public sealed class TranscricaoBaixaQualidadeException()
    : InvalidOperationException(
        "A transcrição local não apresentou conteúdo confiável; a gravação foi preservada para reprocessamento.");

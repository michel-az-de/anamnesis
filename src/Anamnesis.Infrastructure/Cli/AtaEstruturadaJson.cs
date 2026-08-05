using System.Text.Json;
using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;

namespace Anamnesis.Infrastructure.Cli;

public static class AtaEstruturadaJson
{
    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AtaGerada Converter(string json)
    {
        Resultado? resultado;
        try
        {
            resultado = JsonSerializer.Deserialize<Resultado>(json, OpcoesJson);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("A CLI retornou JSON de ata inválido.", exception);
        }

        if (resultado is null)
        {
            throw new InvalidOperationException("A CLI retornou JSON de ata inválido.");
        }

        if (string.IsNullOrWhiteSpace(resultado.ResumoExecutivo))
        {
            throw new InvalidOperationException("A CLI não retornou o resumo executivo da ata.");
        }

        if (resultado.Decisoes is null || resultado.Tarefas is null)
        {
            throw new InvalidOperationException("A CLI não retornou decisões e tarefas da ata.");
        }

        return new AtaGerada(resultado.ResumoExecutivo, resultado.Decisoes, resultado.Tarefas);
    }

    private sealed record Resultado(string? ResumoExecutivo, IReadOnlyList<string>? Decisoes, IReadOnlyList<Tarefa>? Tarefas);
}

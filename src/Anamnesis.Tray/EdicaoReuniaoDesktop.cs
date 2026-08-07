namespace Anamnesis.Tray;

internal sealed class EdicaoReuniaoDesktop
{
    private string _tituloOriginal = string.Empty;
    private string _transcricaoOriginal = string.Empty;

    public Guid? ReuniaoId { get; private set; }

    public bool Editando { get; private set; }

    public bool Sujo => Editando &&
        (!string.Equals(Titulo, _tituloOriginal, StringComparison.Ordinal) ||
         !string.Equals(Transcricao, _transcricaoOriginal, StringComparison.Ordinal));

    public string Titulo { get; private set; } = string.Empty;

    public string Transcricao { get; private set; } = string.Empty;

    public void Iniciar(Guid reuniaoId, string titulo, string transcricao)
    {
        ReuniaoId = reuniaoId;
        _tituloOriginal = titulo;
        _transcricaoOriginal = transcricao;
        Titulo = titulo;
        Transcricao = transcricao;
        Editando = true;
    }

    public void Alterar(string titulo, string transcricao)
    {
        if (!Editando)
        {
            return;
        }

        Titulo = titulo;
        Transcricao = transcricao;
    }

    public void Sincronizar(string titulo, string transcricao)
    {
        if (Sujo)
        {
            return;
        }

        _tituloOriginal = titulo;
        _transcricaoOriginal = transcricao;
        Titulo = titulo;
        Transcricao = transcricao;
    }

    public void Concluir(string titulo, string transcricao)
    {
        _tituloOriginal = titulo;
        _transcricaoOriginal = transcricao;
        Titulo = titulo;
        Transcricao = transcricao;
        Editando = false;
    }

    public void Cancelar()
    {
        Titulo = _tituloOriginal;
        Transcricao = _transcricaoOriginal;
        Editando = false;
    }
}

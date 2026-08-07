namespace Anamnesis.Tray;

internal sealed class LembreteTarefaForm : Form
{
    private readonly DateTimePicker _horario;

    public LembreteTarefaForm(
        string tarefa,
        TemaDesktopPoc tema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tarefa);
        var paleta = DesktopPocPalette.Criar(tema);
        var padrao = DateTime.Today.AddDays(1).AddHours(9);

        Text = "Criar lembrete";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 250);
        BackColor = paleta.Fundo;
        ForeColor = paleta.Texto;
        Font = new Font(DesktopPocDesignTokens.Padrao.Tipografia.Interface, 9.5F);

        var titulo = new Label
        {
            Text = "Quando você quer ser lembrado?",
            Location = new Point(24, 22),
            Size = new Size(470, 26),
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = paleta.Texto
        };
        var descricao = new Label
        {
            Text = tarefa,
            Location = new Point(24, 58),
            Size = new Size(470, 54),
            AutoEllipsis = true,
            ForeColor = paleta.TextoSecundario
        };
        _horario = new DateTimePicker
        {
            Location = new Point(24, 128),
            Size = new Size(260, 32),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy HH:mm",
            Value = padrao
        };
        var confirmar = new Button
        {
            Text = "Criar lembrete",
            DialogResult = DialogResult.OK,
            Location = new Point(326, 188),
            Size = new Size(168, 38),
            BackColor = paleta.Destaque,
            ForeColor = paleta.Fundo,
            FlatStyle = FlatStyle.Flat
        };
        var cancelar = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            Location = new Point(206, 188),
            Size = new Size(110, 38),
            BackColor = paleta.Superficie,
            ForeColor = paleta.Texto,
            FlatStyle = FlatStyle.Flat
        };
        confirmar.FlatAppearance.BorderSize = 1;
        cancelar.FlatAppearance.BorderSize = 1;

        AcceptButton = confirmar;
        CancelButton = cancelar;
        Controls.Add(confirmar);
        Controls.Add(cancelar);
        Controls.Add(_horario);
        Controls.Add(descricao);
        Controls.Add(titulo);
    }

    public DateTimeOffset Horario => new(_horario.Value);

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK && Horario <= DateTimeOffset.Now)
        {
            e.Cancel = true;
            MessageBox.Show(
                "Escolha uma data e hora futuras.",
                "Horário inválido",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        base.OnFormClosing(e);
    }
}

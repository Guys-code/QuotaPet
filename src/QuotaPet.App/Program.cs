namespace QuotaPet.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var instance = new Mutex(true, "QuotaPet.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("QuotaPet 已经在运行。", "QuotaPet",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new PetForm());
    }
}

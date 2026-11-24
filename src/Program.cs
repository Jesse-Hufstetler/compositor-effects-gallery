namespace Gallery;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var options = Options.Parse(args);
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
            Native.EnsureDispatcherQueue();
            if (args is ["--probe", var path])
            {
                Probe.Run(path);
                return;
            }
            Application.Run(new GalleryForm(options));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.txt"), ex.ToString());
            if (!options.NoActivate) MessageBox.Show(ex.ToString(), "Compositor effects gallery");
        }
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using FocusDesk.Core;

namespace FocusDesk.App;
public partial class App : Application
{
    Mutex? instance;
    public static string DataDirectory { get; private set; } = "";
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root.Parent!=null && !File.Exists(Path.Combine(root.FullName,"FocusDesk.slnx"))) root=root.Parent;
        DataDirectory=Environment.GetEnvironmentVariable("FOCUSDESK_DATA") ?? Path.Combine(File.Exists(Path.Combine(root.FullName,"FocusDesk.slnx"))?root.FullName:AppContext.BaseDirectory,"Data");
        try
        {
            Directory.CreateDirectory(DataDirectory);
            string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(DataDirectory).ToUpperInvariant())))[..16];
            instance=new Mutex(true,"Local\\FocusDesk-"+key,out bool first);
            if(!first) { MessageBox.Show("FocusDesk уже открыт. Переключитесь на его окно.","FocusDesk"); Shutdown(); return; }
            var store=new DeskStore(Path.Combine(DataDirectory,"focusdesk.db")); store.Initialize();
            var window=new MainWindow(store); MainWindow=window;
            if(e.Args.Contains("--verify-ui"))
            {
                ShutdownMode=ShutdownMode.OnExplicitShutdown;
                try { window.VerifyUi(e.Args.Last()); Shutdown(0); }
                catch(Exception ex) { File.WriteAllText(Path.Combine(DataDirectory,"ui-error.txt"),ex.ToString()); Shutdown(1); }
                return;
            }
            window.Show();
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(DataDirectory,"startup-error.txt"),ex.ToString());
            MessageBox.Show("Не удалось открыть FocusDesk. Подробности сохранены в Data/startup-error.txt.\n\n"+ex.Message,"FocusDesk",MessageBoxButton.OK,MessageBoxImage.Error); Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}

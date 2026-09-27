using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Svetolesye.Core;
namespace Svetolesye.Game;
public partial class App : Application
{
    Mutex? mutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root.Parent!=null&&!File.Exists(Path.Combine(root.FullName,"Svetolesye.slnx")))root=root.Parent;
        string directory=Environment.GetEnvironmentVariable("SVETOLESYE_DATA")??Path.Combine(File.Exists(Path.Combine(root.FullName,"Svetolesye.slnx"))?root.FullName:AppContext.BaseDirectory,"Data");
        try
        {
            Directory.CreateDirectory(directory);
            mutex=new Mutex(true,"Local\\Svetolesye-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToUpperInvariant())))[..12],out bool first);
            if(!first){MessageBox.Show("Светолесье уже открыто. Переключись на его окно.","Светолесье");Shutdown();return;}
            var window=new MainWindow(new SaveStore(Path.Combine(directory,"journey.json")));MainWindow=window;
            if(e.Args.Contains("--render-check"))
            {
                if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SVETOLESYE_DATA")))throw new InvalidOperationException("Use a disposable SVETOLESYE_DATA directory for checks.");
                ShutdownMode=ShutdownMode.OnExplicitShutdown;
                try{window.RenderCheck(e.Args.Last());Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(directory,"render-error.txt"),ex.ToString());Shutdown(1);}return;
            }
            window.Show();
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(directory,"error.txt"),ex.ToString());MessageBox.Show("Не удалось открыть игру: "+ex.Message,"Светолесье");Shutdown(1);}
    }
    protected override void OnExit(ExitEventArgs e){mutex?.Dispose();base.OnExit(e);}
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FocusDesk.App;
public sealed class EditorWindow : Window
{
    public StackPanel Body {get;}=new();
    public Action? OnSave {get;set;}
    readonly TextBlock error=new(){Foreground=Brushes.Firebrick,TextWrapping=TextWrapping.Wrap,Margin=new(0,12,0,0),Visibility=Visibility.Collapsed};
    public EditorWindow(string title,string subtitle)
    {
        Icon=new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/FocusDesk.ico"));
        Title=title;Width=510;Height=760;MinWidth=450;MinHeight=550;MaxHeight=SystemParameters.WorkArea.Height-30;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;
        var root=new DockPanel{Margin=new(28)};Content=root;
        var heading=new StackPanel {Margin=new(0,0,0,20)};heading.Children.Add(new TextBlock{Text=title,FontSize=24,FontWeight=FontWeights.SemiBold});heading.Children.Add(new TextBlock{Text=subtitle,FontSize=13,Foreground=Brushes.Gray,Margin=new(0,8,0,0)});DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var footer=new StackPanel();footer.Children.Add(error);
        var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,18,0,0)};
        var cancel=new Button {Content="Отмена",IsCancel=true,Margin=new(0,0,10,0)};cancel.Click+=(_,_)=>DialogResult=false;
        var save=new Button {Content="Сохранить",Style=(Style)FindResource("Primary")};save.Click+=(_,_)=>{try{OnSave?.Invoke();DialogResult=true;}catch(Exception ex){ShowError(ex.Message);}};
        buttons.Children.Add(cancel);buttons.Children.Add(save);footer.Children.Add(buttons);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        root.Children.Add(new ScrollViewer{Content=Body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        Loaded+=(_,_)=>{if(Body.Children.Count>1)Body.Children[1].Focus();};
    }
    public void Field(string label,FrameworkElement element)
    {
        Body.Children.Add(new TextBlock{Text=label,FontSize=12,Foreground=Brushes.Gray,Margin=new(0,12,0,7)});element.Margin=new(0,0,0,4);System.Windows.Automation.AutomationProperties.SetName(element,label);Body.Children.Add(element);
    }
    public TextBox Input(string label,string value,int max)
    {
        var box=new TextBox{Text=value,MaxLength=max};Field(label,box);return box;
    }
    public void ShowError(string text){error.Text=text;error.Visibility=Visibility.Visible;}
}

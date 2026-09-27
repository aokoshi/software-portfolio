using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FocusDesk.Core;
using Microsoft.Win32;

namespace FocusDesk.App;

public partial class MainWindow : Window
{
    readonly DeskStore store;
    readonly Stopwatch monotonic=Stopwatch.StartNew();
    readonly FocusClock clock;
    readonly DispatcherTimer ticker=new() { Interval=TimeSpan.FromSeconds(1) };
    TimerDraft? draft;
    List<Project> projects=[];
    List<WorkItem> tasks=[];
    List<FocusSession> sessions=[];
    readonly Dictionary<string,Button> navButtons=[];
    string current="today", search="";
    long projectFilter;
    int statusFilter=-1;
    bool showArchived;
    StackPanel? taskList;
    TextBlock? timeDisplay, timerCaption;
    Button? timerToggle;
    ComboBox? timerTask, duration;
    int checkpoint;
    static readonly string[] Statuses=["Запланировано","В работе","Готово"];
    static readonly string[] Priorities=["Низкий","Обычный","Высокий"];
    static readonly CultureInfo Ru=CultureInfo.GetCultureInfo("ru-RU");
    public MainWindow(DeskStore store)
    {
        InitializeComponent(); this.store=store;
        clock=new FocusClock(()=>monotonic.Elapsed.TotalSeconds);
        Reload(); draft=store.Draft();
        if(draft!=null) { clock.Configure(draft.TargetSeconds,draft.ElapsedSeconds); StatusLine.Text="Восстановлена незавершённая сессия. Продолжи её в разделе «Фокус»."; }
        foreach(var (key,glyph,label) in new[]{("today","◉","Сегодня"),("tasks","☰","Мои задачи"),("board","▦","Доска"),("projects","▣","Проекты"),("focus","◷","Фокус"),("stats","▥","Статистика"),("settings","⚙","Настройки")})
        {
            var b=Btn(glyph+"   "+label,()=>Navigate(key)); b.HorizontalContentAlignment=HorizontalAlignment.Left; b.Margin=new(0,0,0,7); b.Padding=new(16,12,8,12); b.BorderThickness=new(0); navButtons[key]=b; Navigation.Children.Add(b);
        }
        ticker.Tick+=(_,_)=>Tick(); ticker.Start();
        Closing+=OnClosing; Closed+=(_,_)=>{ticker.Stop();SystemEvents.PowerModeChanged-=PowerChanged;};
        SystemEvents.PowerModeChanged+=PowerChanged;
        Navigate("today");
    }
    static Brush Paint(string color)=>(Brush)new BrushConverter().ConvertFromString(color)!;
    static TextBlock Text(string value,double size=14,string color="#242436",bool bold=false) => new() { Text=value,FontSize=size,Foreground=Paint(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap };
    static StackPanel Stack(params UIElement[] children) { var p=new StackPanel();foreach(var c in children)p.Children.Add(c);return p; }
    static StackPanel Row(params UIElement[] children) { var p=Stack(children);p.Orientation=Orientation.Horizontal;return p; }
    static Border Card(UIElement child,string background="#FFFFFF",double padding=20) => new() { Background=Paint(background),CornerRadius=new(14),BorderBrush=Paint("#E8E6F0"),BorderThickness=new(1),Padding=new(padding),Child=child,Margin=new(0,0,0,14) };
    Button Btn(string label,Action action,bool primary=false)
    {
        var b=new Button {Content=label,Margin=new(0,0,8,0)};
        if(primary)b.Style=(Style)FindResource("Primary");
        b.Click+=(_,_)=>Guard(action);return b;
    }
    void Guard(Action action)
    {
        try { action(); }
        catch(Exception ex) { StatusLine.Text="Не удалось выполнить действие: "+ex.Message; MessageBox.Show(this,ex.Message,"FocusDesk",MessageBoxButton.OK,MessageBoxImage.Warning); }
    }
    static UIElement Gap(double height=16)=>new Border {Height=height};
    static Grid Columns(params (UIElement item,double weight)[] values)
    {
        var grid=new Grid();int i=0;
        foreach(var (item,weight) in values) {grid.ColumnDefinitions.Add(new(){Width=new(weight,GridUnitType.Star)});Grid.SetColumn(item,i++); if(item is FrameworkElement f)f.Margin=new(i>1?16:0,0,0,Math.Max(16,f.Margin.Bottom));grid.Children.Add(item);}return grid;
    }
    void Reload() {projects=store.Projects();tasks=store.Tasks();sessions=store.Sessions();}
    void Changed(string message) {Reload();Navigate(current);StatusLine.Text=message;}
    void Header(string eyebrow,string title,string subtitle,Button? button=null)
    {
        var panel=new DockPanel {Margin=new(0,0,0,24)};
        if(button!=null) {DockPanel.SetDock(button,Dock.Right);button.VerticalAlignment=VerticalAlignment.Center;panel.Children.Add(button);}
        var head=Stack(Text(eyebrow,10,"#9286BD",true),Gap(8),Text(title,30,"#292638",true),Gap(8),Text(subtitle,13,"#8B889A"));panel.Children.Add(head);Page.Children.Add(panel);
    }
    void Navigate(string page)
    {
        current=page;Page.Children.Clear();timeDisplay=null;timerCaption=null;timerToggle=null;timerTask=null;duration=null;
        foreach(var (key,b) in navButtons) {b.Background=Paint(key==page?"#EDE8FC":"#FFFFFF");b.Foreground=Paint(key==page?"#7160D5":"#827D93");b.FontWeight=key==page?FontWeights.SemiBold:FontWeights.Normal;}
        Breadcrumb.Text="Рабочее пространство / "+navButtons[page].Content.ToString()![4..];
        switch(page) {case "today":Today();break;case "tasks":TaskPage();break;case "board":Board();break;case "projects":ProjectsPage();break;case "focus":FocusPage();break;case "stats":Stats();break;case "settings":Settings();break;}
        UpdateClockView();
    }
    IEnumerable<WorkItem> ActiveTasks=>tasks.Where(t=>!t.Archived);
    Project ProjectFor(WorkItem task)=>projects.First(p=>p.Id==task.ProjectId);
    static string TimeLabel(int seconds)=>seconds<60?$"{seconds} сек":seconds<3600?$"{seconds/60} мин":$"{seconds/3600} ч {seconds%3600/60} мин";
    Border Metric(string label,string value,string detail,string color="#7160D5")
    {
        var panel=Stack(Text(label,12,"#888497"),Gap(14),Text(value,30,color,true),Gap(9),Text(detail,11,"#A09AAC"));return Card(panel);
    }
    void Today()
    {
        Header(DateTime.Now.ToString("dddd, d MMMM",Ru).ToUpper(Ru),"Место для важных дел","Спокойный план на день. Начни с одной задачи.",Btn("＋  Новая задача",()=>EditTask(),true));
        var today=DateOnly.FromDateTime(DateTime.Now);
        int todaySeconds=sessions.Where(s=>s.EndedAt.LocalDateTime.Date==DateTime.Today).Sum(s=>s.Seconds);
        int todayDone=ActiveTasks.Count(t=>t.Status==WorkStatus.Done&&t.CompletedAt?.LocalDateTime.Date==DateTime.Today);
        Page.Children.Add(Columns((Metric("В фокусе сегодня",TimeLabel(todaySeconds),"Каждая минута имеет значение"),1),(Metric("Завершено сегодня",todayDone.ToString(),"Маленькие шаги к большому", "#2E8B77"),1),(Metric("Активные задачи",ActiveTasks.Count(t=>t.Status!=WorkStatus.Done).ToString(),$"В {projects.Count} проектах","#C28B50"),1)));
        var upcoming=ActiveTasks.Where(t=>t.Status!=WorkStatus.Done&&(t.DueDate<=today||t.Status==WorkStatus.Active)).OrderBy(t=>t.DueDate??DateOnly.MaxValue).ThenByDescending(t=>t.Priority).Take(5).ToList();
        var left=Stack(Text("Твой план на сегодня",18,bold:true),Gap(5),Text("Задачи в работе и ближайшие сроки",12,"#9892A6"),Gap(16));
        if(upcoming.Count==0)left.Children.Add(Empty("Сегодня можно выдохнуть","Добавь задачу на сегодня или выбери любую на доске."));
        foreach(var t in upcoming)left.Children.Add(TaskCard(t,compact:true));
        var right=Stack();
        var focus=Stack(Text("ВРЕМЯ ДЛЯ ФОКУСА",10,"#8D7BBD",true),Gap(14),Text("25 минут\nдля одной задачи",25,"#514375",true),Gap(12),Text("Убери лишние вкладки.\nВыбери то, что важно сейчас.",13,"#8F82A3"),Gap(22),Btn(draft==null?"Начать сессию  →":"Вернуться к таймеру  →",()=>Navigate("focus"),true));
        right.Children.Add(Card(focus,"#F0EBFA",24));
        int overdue=ActiveTasks.Count(t=>t.Status!=WorkStatus.Done&&t.DueDate<today);
        right.Children.Add(Card(Stack(Text("Проверка сроков",15,bold:true),Gap(12),Text(overdue==0?"Всё в своём темпе":$"Просрочено задач: {overdue}",16,overdue==0?"#4E927E":"#BE765F",true),Gap(8),Text(overdue==0?"Задач с прошедшим сроком нет.":"Пересмотри сроки и выбери следующий шаг.",12,"#95909F"))));
        Page.Children.Add(Columns((left,1.6),(right,1)));
        if(projects.Count==0) Page.Children.Add(Card(Stack(Text("Начнём с твоего пространства",18,bold:true),Gap(8),Text("Создай первый проект или загрузи примеры задач для знакомства с приложением.",13,"#8B849A"),Gap(16),Row(Btn("Создать проект",()=>EditProject(),true),Btn("Добавить примеры",()=>{store.SeedExamples();Changed("Добавлены примеры задач. Их можно изменить или отправить в архив.");})))));
    }
    UIElement Empty(string title,string detail)=>Card(Stack(Text(title,16,"#91869D",true),Gap(8),Text(detail,13,"#A29AAE")),"#FAF9FC");
    Border TaskCard(WorkItem t,bool compact=false,bool board=false)
    {
        var project=ProjectFor(t);
        var content=Stack();
        var meta=new DockPanel();var priority=Text(t.Priority==Priority.High?"↑ Высокий":t.Priority==Priority.Low?"↓ Низкий":"Обычный",10,t.Priority==Priority.High?"#C67B5C":"#AAA2B4");DockPanel.SetDock(priority,Dock.Right);meta.Children.Add(priority);meta.Children.Add(Text("●  "+project.Name,11,project.Color,true));content.Children.Add(meta);content.Children.Add(Gap(11));
        content.Children.Add(Text(t.Title,compact?14:15,"#393345",true));
        if(!compact&&!string.IsNullOrWhiteSpace(t.Notes)){var note=Text(t.Notes,12,"#9B92A7");note.MaxHeight=36;note.TextTrimming=TextTrimming.CharacterEllipsis;note.Margin=new(0,8,0,0);content.Children.Add(note);}
        string due=t.DueDate?.ToString("d MMM",Ru)??"Без срока";
        bool late=t.DueDate<DateOnly.FromDateTime(DateTime.Now)&&t.Status!=WorkStatus.Done;
        content.Children.Add(Gap(12));content.Children.Add(Text((late?"Просрочено · ":"◷  ")+due+(!board?"    ·    "+Statuses[(int)t.Status]:""),11,late?"#C27665":"#AAA0B6"));
        content.Children.Add(Gap(12));
        var actions=new WrapPanel();
        var edit=Btn("Изменить",()=>EditTask(t));edit.Padding=new(9,5,9,5);edit.FontSize=11;actions.Children.Add(edit);
        if(t.Archived){var restore=Btn("Вернуть",()=>{store.Archive(t.Id,false);Changed("Задача возвращена из архива.");});restore.Padding=new(9,5,9,5);restore.FontSize=11;actions.Children.Add(restore);}
        else
        {
            var move=Btn(t.Status==WorkStatus.Done?"↶ В план":t.Status==WorkStatus.Active?"✓ Готово":"→ В работу",()=>{store.Move(t,t.Status==WorkStatus.Done?WorkStatus.Planned:t.Status==WorkStatus.Active?WorkStatus.Done:WorkStatus.Active);Changed("Статус задачи сохранён.");});move.Padding=new(9,5,9,5);move.FontSize=11;actions.Children.Add(move);
            if(!compact&&!board&&t.Status!=WorkStatus.Done) {var f=Btn("Фокус",()=>StartForTask(t));f.Padding=new(9,5,9,5);f.FontSize=11;actions.Children.Add(f);}
        }
        content.Children.Add(actions);return Card(content,padding:16);
    }
    ComboBox ProjectSelector(bool all,long selected=0)
    {
        var combo=new ComboBox {MinWidth=160,MaxWidth=250,Margin=new(0,0,12,0),DisplayMemberPath="Name",SelectedValuePath="Id"};
        var choices=new List<Project>();if(all)choices.Add(new(0,"Все проекты",DeskStore.Colors[0]));choices.AddRange(projects);combo.ItemsSource=choices;combo.SelectedValue=selected;if(combo.SelectedIndex<0&&choices.Count>0)combo.SelectedIndex=0;return combo;
    }
    void TaskPage()
    {
        Header("СВОБОДНАЯ ГОЛОВА","Мои задачи","Все идеи и обязательства — в одном месте.",Btn("＋  Новая задача",()=>EditTask(),true));
        var filters=new WrapPanel {Margin=new(0,0,0,20)};
        var searchBox=new TextBox {Text=search,Width=220,Margin=new(0,0,12,0),ToolTip="Поиск по названию и описанию"};System.Windows.Automation.AutomationProperties.SetName(searchBox,"Поиск задач");
        var project=ProjectSelector(true,projectFilter);
        var status=new ComboBox {ItemsSource=new[]{"Все статусы"}.Concat(Statuses),SelectedIndex=statusFilter+1,Width=165,Margin=new(0,0,12,0)};
        var archive=new CheckBox {Content="Архив",IsChecked=showArchived,VerticalAlignment=VerticalAlignment.Center,Margin=new(8,0,0,0)};
        filters.Children.Add(Stack(Text("Поиск по названию и описанию",11,"#93899F"),Gap(6),searchBox));filters.Children.Add(Stack(Text("Проект",11,"#93899F"),Gap(6),project));filters.Children.Add(Stack(Text("Статус",11,"#93899F"),Gap(6),status));filters.Children.Add(archive);Page.Children.Add(filters);
        taskList=new StackPanel();Page.Children.Add(taskList);
        searchBox.TextChanged+=(_,_)=>{search=searchBox.Text;RenderTaskList();};project.SelectionChanged+=(_,_)=>{projectFilter=(long?)project.SelectedValue??0;RenderTaskList();};status.SelectionChanged+=(_,_)=>{statusFilter=status.SelectedIndex-1;RenderTaskList();};archive.Click+=(_,_)=>{showArchived=archive.IsChecked==true;RenderTaskList();};RenderTaskList();
    }
    void RenderTaskList()
    {
        if(taskList==null)return;taskList.Children.Clear();
        var filtered=tasks.Where(t=>t.Archived==showArchived&&(projectFilter==0||t.ProjectId==projectFilter)&&(statusFilter<0||(int)t.Status==statusFilter)&&
            (t.Title.Contains(search.Trim(),StringComparison.CurrentCultureIgnoreCase)||t.Notes.Contains(search.Trim(),StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(t=>t.Status==WorkStatus.Done).ThenBy(t=>t.DueDate??DateOnly.MaxValue).ThenByDescending(t=>t.Priority).ToList();
        taskList.Children.Add(Text($"Найдено задач: {filtered.Count}",11,"#9D93AA"));taskList.Children.Add(Gap(12));
        if(filtered.Count==0)taskList.Children.Add(Empty("Здесь пока пусто","Попробуй изменить фильтры или добавить задачу."));
        foreach(var t in filtered)taskList.Children.Add(TaskCard(t));
    }
    void Board()
    {
        Header("ОТ ИДЕИ К РЕЗУЛЬТАТУ","Доска задач","Продвигай задачи кнопками на карточке. Каждый шаг виден.",Btn("＋  Новая задача",()=>EditTask(),true));
        var filter=ProjectSelector(true,projectFilter);filter.HorizontalAlignment=HorizontalAlignment.Left;filter.Margin=new(0,0,0,20);filter.SelectionChanged+=(_,_)=>{projectFilter=(long?)filter.SelectedValue??0;Navigate("board");};Page.Children.Add(filter);
        var columns=new List<(UIElement,double)>();
        for(int i=0;i<3;i++)
        {
            var list=ActiveTasks.Where(t=>(int)t.Status==i&&(projectFilter==0||t.ProjectId==projectFilter)).OrderByDescending(t=>t.Priority).ToList();
            var column=Stack(Text("●  "+Statuses[i]+"   "+list.Count,14,i==0?"#A794BB":i==1?"#7160D5":"#4F9B80",true),Gap(18));
            foreach(var task in list)column.Children.Add(TaskCard(task,board:true));
            if(list.Count==0)column.Children.Add(Empty("Нет задач",i==2?"Здесь будут твои результаты.":"Задачи появятся здесь после смены статуса."));
            columns.Add((column,1));
        }
        Page.Children.Add(Columns(columns.ToArray()));
    }
    void ProjectsPage()
    {
        Header("КАЖДОМУ ДЕЛУ — МЕСТО","Проекты","Раздели учёбу, работу и личные планы.",Btn("＋  Новый проект",()=>EditProject(),true));
        if(projects.Count==0)Page.Children.Add(Empty("Первый проект начинается здесь","Например: учёба, портфолио или личные планы."));
        foreach(var p in projects)
        {
            var items=ActiveTasks.Where(t=>t.ProjectId==p.Id).ToList();int done=items.Count(t=>t.Status==WorkStatus.Done);
            var head=new DockPanel();var edit=Btn("Изменить",()=>EditProject(p));DockPanel.SetDock(edit,Dock.Right);head.Children.Add(edit);head.Children.Add(Text("●  "+p.Name,20,p.Color,true));
            var bar=new ProgressBar {Maximum=Math.Max(1,items.Count),Value=done,Foreground=Paint(p.Color),Margin=new(0,18,0,12)};
            var body=Stack(head,bar,Text($"{done} из {items.Count} задач завершено",12,"#9A91A4"),Gap(14),Btn("Открыть задачи  →",()=>{projectFilter=p.Id;statusFilter=-1;search="";showArchived=false;Navigate("tasks");}));Page.Children.Add(Card(body));
        }
    }
    void FocusPage()
    {
        Header("ОДНА ЗАДАЧА. ОДИН ШАГ.","Время сфокусироваться","Сессии сохраняются в статистике. Пауза не учитывается во времени.");
        var panel=Stack();panel.MaxWidth=570;panel.HorizontalAlignment=HorizontalAlignment.Center;
        timerTask=new ComboBox {DisplayMemberPath="Title",SelectedValuePath="Id",Margin=new(0,8,0,18)};
        var choices=new List<TaskChoice>{new(0,"Свободная сессия")};choices.AddRange(ActiveTasks.Where(t=>t.Status!=WorkStatus.Done).Select(t=>new TaskChoice(t.Id,t.Title)));
        if(draft?.TaskId is long id&&!choices.Any(t=>t.Id==id))choices.Add(new(id,draft.TaskTitle));
        timerTask.ItemsSource=choices;timerTask.SelectedValue=draft?.TaskId??0;timerTask.IsEnabled=draft==null;
        panel.Children.Add(Text("Над чем поработаем?",12,"#8B8199",true));panel.Children.Add(timerTask);
        var face=new Grid {Width=260,Height=260,Margin=new(0,8,0,18)};
        face.Children.Add(new System.Windows.Shapes.Ellipse{Fill=Paint("#F7F4FF"),Stroke=Paint("#E0D8F3"),StrokeThickness=8});
        var numbers=Stack();numbers.VerticalAlignment=VerticalAlignment.Center;
        timeDisplay=Text("25:00",58,"#66529C",true);timeDisplay.TextAlignment=TextAlignment.Center;
        timerCaption=Text("Готов к началу",12,"#AC9BC1");timerCaption.TextAlignment=TextAlignment.Center;
        numbers.Children.Add(timeDisplay);numbers.Children.Add(Gap(4));numbers.Children.Add(timerCaption);face.Children.Add(numbers);panel.Children.Add(face);
        duration=new ComboBox {ItemsSource=new[]{15,25,45,60},SelectedItem=draft?.TargetSeconds/60??25,Width=105,IsEnabled=draft==null};
        if(duration.SelectedIndex<0)duration.SelectedIndex=1; duration.SelectionChanged+=(_,_)=>UpdateClockView();
        panel.Children.Add(Row(Text("Длительность, мин   ",13,"#8D829E"),duration));panel.Children.Add(Gap(22));
        timerToggle=Btn(clock.Running?"Ⅱ  Пауза":draft!=null?"▶  Продолжить":"▶  Начать фокус",ToggleTimer,true);timerToggle.MinWidth=185;
        panel.Children.Add(Row(timerToggle,Btn("Завершить и сохранить",FinishEarly)));panel.Children.Add(Gap(20));
        var note=Text("Сосредоточься на процессе.\nВо время паузы можно спокойно переключиться.",12,"#A397B0");note.TextAlignment=TextAlignment.Center;panel.Children.Add(note);
        Page.Children.Add(Card(panel,padding:30));
        Page.Children.Add(Text("ПОСЛЕДНИЕ СЕССИИ",11,"#A393B3",true));Page.Children.Add(Gap(12));
        foreach(var s in sessions.Take(4))Page.Children.Add(SessionRow(s));
        if(sessions.Count==0)Page.Children.Add(Text("Здесь появится твоя первая сессия концентрации.",13,"#A397B0"));
    }
    record TaskChoice(long Id,string Title);
    void StartForTask(WorkItem task)
    {
        if(draft!=null) {Navigate("focus");StatusLine.Text="Сначала заверши текущую сессию, чтобы выбрать другую задачу.";return;}
        Navigate("focus");timerTask!.SelectedValue=task.Id;
    }
    void ToggleTimer()
    {
        if(clock.Running) {clock.Pause();PersistDraft();StatusLine.Text="Таймер на паузе. Время паузы не учитывается.";}
        else
        {
            if(draft==null)
            {
                int minutes=(int?)duration?.SelectedItem??25;
                var selected=timerTask?.SelectedItem as TaskChoice??new(0,"Свободная сессия");
                clock.Configure(minutes*60);
                draft=new(Guid.NewGuid().ToString(),selected.Id==0?null:selected.Id,selected.Title,DateTimeOffset.UtcNow,minutes*60,0);
                try {store.SaveDraft(draft);}catch {draft=null;throw;}
            }
            clock.Start();StatusLine.Text="Сессия идёт. Можно работать в других разделах или свернуть окно.";
        }
        UpdateClockView();
    }
    void PersistDraft()
    {
        if(draft==null)return;draft=draft with {ElapsedSeconds=(int)clock.Elapsed};store.SaveDraft(draft);
    }
    void Tick()
    {
        try
        {
            if(draft!=null&&clock.Running)
            {
                if(clock.Finished) {CompleteSession(true);StatusLine.Text="Сессия завершена! Сделай небольшой перерыв.";System.Media.SystemSounds.Asterisk.Play();}
                else if(++checkpoint>=5){checkpoint=0;PersistDraft();}
            }
            UpdateClockView();
        }
        catch(Exception ex){clock.Pause();StatusLine.Text="Таймер приостановлен: не удалось сохранить данные. "+ex.Message;UpdateClockView();}
    }
    void UpdateClockView()
    {
        int remaining=draft==null?((int?)duration?.SelectedItem??25)*60:clock.Remaining;
        string digits=$"{remaining/60:00}:{remaining%60:00}";
        MiniTimer.Text=draft==null?"Твоё время для важного":(clock.Running?"◷  ":"Ⅱ  ")+digits;
        if(timeDisplay!=null)timeDisplay.Text=digits;
        if(timerCaption!=null)timerCaption.Text=draft==null?"Готов к началу":clock.Running?"Время для важного":"Пауза — тоже часть работы";
        if(timerToggle!=null)timerToggle.Content=clock.Running?"Ⅱ  Пауза":draft!=null?"▶  Продолжить":"▶  Начать фокус";
        if(timerTask!=null)timerTask.IsEnabled=draft==null;
        if(duration!=null)duration.IsEnabled=draft==null;
    }
    void FinishEarly()
    {
        if(draft==null){StatusLine.Text="Сначала начни сессию.";return;}
        CompleteSession(clock.Finished);StatusLine.Text="Сессия сохранена. Учитывается только время работы.";
    }
    void CompleteSession(bool completed)
    {
        if(draft==null)return;clock.Pause();int elapsed=(int)clock.Elapsed;
        if(elapsed>0)store.SaveSession(new(draft.Id,draft.TaskId,draft.TaskTitle,draft.StartedAt,DateTimeOffset.UtcNow,elapsed,completed));else store.ClearDraft();
        draft=null;clock.Configure(25*60);Reload();Navigate(current);
    }
    void PowerChanged(object sender,PowerModeChangedEventArgs e)
    {
        if(e.Mode==PowerModes.Suspend) Dispatcher.Invoke(()=>{if(clock.Running)Guard(()=>{clock.Pause();PersistDraft();UpdateClockView();StatusLine.Text="Сессия приостановлена перед переходом компьютера в сон.";});});
    }
    void OnClosing(object? sender,CancelEventArgs e)
    {
        if(draft==null)return;
        try {clock.Pause();PersistDraft();}
        catch(Exception ex){e.Cancel=true;MessageBox.Show(this,"Не удалось сохранить таймер: "+ex.Message,"FocusDesk");}
    }
    Border SessionRow(FocusSession s)
    {
        var row=new DockPanel();var total=Text(TimeLabel(s.Seconds),16,"#7160D5",true);DockPanel.SetDock(total,Dock.Right);row.Children.Add(total);
        row.Children.Add(Stack(Text(s.TaskTitle,13,bold:true),Gap(6),Text(s.EndedAt.LocalDateTime.ToString("d MMM, HH:mm",Ru)+" · "+(s.Completed?"Полная сессия":"Завершена раньше"),11,"#A394B0")));return Card(row,padding:16);
    }
    void Stats()
    {
        Header("ЗАМЕЧАЙ СВОЙ ПРОГРЕСС","Статистика","Последние семь дней: время концентрации и завершённые задачи.");
        var first=DateTime.Today.AddDays(-6);var week=sessions.Where(s=>s.EndedAt.LocalDateTime.Date>=first&&s.EndedAt.LocalDateTime.Date<=DateTime.Today).ToList();
        Page.Children.Add(Columns((Metric("Время в фокусе",TimeLabel(week.Sum(s=>s.Seconds)),"За последние 7 дней"),1),(Metric("Сессий",week.Count.ToString(),$"Полных сессий: {week.Count(s=>s.Completed)}","#2E8B77"),1),(Metric("Завершено задач",tasks.Count(t=>t.Status==WorkStatus.Done&&t.CompletedAt?.LocalDateTime.Date>=first&&t.CompletedAt?.LocalDateTime.Date<=DateTime.Today).ToString(),"Включая задачи в архиве","#C28B50"),1)));
        var chart=Stack(Text("Ритм недели",18,bold:true),Gap(22));
        int max=Math.Max(60,Enumerable.Range(0,7).Select(i=>week.Where(s=>s.EndedAt.LocalDateTime.Date==first.AddDays(i)).Sum(s=>s.Seconds)).Max());
        for(int i=0;i<7;i++)
        {
            var day=first.AddDays(i);int value=week.Where(s=>s.EndedAt.LocalDateTime.Date==day).Sum(s=>s.Seconds);
            var line=new Grid {Margin=new(0,0,0,18)};line.ColumnDefinitions.Add(new(){Width=new(105)});line.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});line.ColumnDefinitions.Add(new(){Width=new(105)});
            line.Children.Add(Text(day.ToString("ddd, d MMM",Ru),12,"#93889E"));
            var bar=new ProgressBar {Maximum=max,Value=value,Height=14,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(bar,1);line.Children.Add(bar);
            var label=Text(TimeLabel(value),12,"#8D7DA2");label.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(label,2);line.Children.Add(label);chart.Children.Add(line);
        }
        chart.Children.Add(Text("Время сессии относится к дню её завершения. Паузы исключены.",11,"#A395AE"));Page.Children.Add(Card(chart));
        Page.Children.Add(Text("ИСТОРИЯ СЕССИЙ · последние 30",11,"#A395AE",true));Page.Children.Add(Gap(14));
        foreach(var s in sessions.Take(30))Page.Children.Add(SessionRow(s));
        if(sessions.Count==0)Page.Children.Add(Empty("История начинается с первой сессии","Перейди в «Фокус», выбери задачу и запусти таймер."));
    }
    void Settings()
    {
        Header("ТВОЁ ПРОСТРАНСТВО","Настройки и данные","Работай без подключения к интернету. Сохраняй резервные копии.");
        var path=new TextBox {Text=store.DatabasePath,IsReadOnly=true,Margin=new(0,12,0,18)};
        Page.Children.Add(Card(Stack(Text("Локальная база данных",18,bold:true),Gap(8),Text("Проекты, задачи и сессии хранятся в одном файле SQLite.",13,"#95859F"),path,
            Row(Btn("Создать резервную копию",Backup,true),Btn("Восстановить из копии",Restore)),Gap(16),Text("Перед восстановлением сохраняется копия текущих данных в Data/Backups.",12,"#A08CAC"))));
        Page.Children.Add(Card(Stack(Text("Как пользоваться FocusDesk",18,bold:true),Gap(14),Text("1. Создай проект для учёбы, работы или личных дел.\n\n2. Добавь задачи и назначь сроки. На доске меняй их статус.\n\n3. Запусти сессию концентрации. Свернуть окно можно — таймер продолжит идти.\n\n4. Заверши сессию и посмотри свой прогресс в статистике.",14,"#92809F"))));
        Page.Children.Add(Text("FocusDesk 1.0 · C# / WPF / SQLite\nЛокальное приложение для Windows. Данные не отправляются на сервер.",12,"#A08CAC"));
    }
    void Backup()
    {
        if(draft!=null)PersistDraft();
        var dialog=new SaveFileDialog {Title="Резервная копия FocusDesk",Filter="База FocusDesk (*.db)|*.db",FileName=$"FocusDesk-{DateTime.Now:yyyy-MM-dd-HHmm}.db",InitialDirectory=App.DataDirectory};
        if(dialog.ShowDialog(this)==true){store.Backup(dialog.FileName);StatusLine.Text="Резервная копия сохранена: "+dialog.FileName;}
    }
    void Restore()
    {
        if(draft!=null)throw new ArgumentException("Сначала завершите текущую сессию в разделе «Фокус».");
        var dialog=new OpenFileDialog {Title="Выберите резервную копию FocusDesk",Filter="База FocusDesk (*.db)|*.db"};
        if(dialog.ShowDialog(this)!=true)return;
        if(MessageBox.Show(this,"Заменить проекты, задачи и статистику данными из выбранной копии? Текущие данные будут сохранены отдельно.","Восстановление данных",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        string safety=store.Restore(dialog.FileName);projectFilter=0;Changed("Данные восстановлены. Предыдущая версия: "+safety);
    }
    void EditProject(Project? project=null)
    {
        var form=new EditorWindow(project==null?"Новый проект":"Изменить проект","Отдельное пространство для связанных задач.") {Owner=this};
        var name=form.Input("Название",project?.Name??"",60);
        var colors=new ComboBox {ItemsSource=DeskStore.Colors.Select((c,i)=>new ColorChoice(c,new[]{"Лаванда","Шалфей","Карамель","Небо","Роза"}[i])).ToList(),DisplayMemberPath="Name",SelectedValuePath="Color",SelectedValue=project?.Color??DeskStore.Colors[0]};form.Field("Цвет проекта",colors);
        form.OnSave=()=>{store.SaveProject(project?.Id??0,name.Text,(string)colors.SelectedValue);};
        if(form.ShowDialog()==true)Changed("Проект сохранён.");
    }
    record ColorChoice(string Color,string Name);
    void EditTask(WorkItem? task=null)
    {
        if(projects.Count==0){EditProject();if(projects.Count==0)return;}
        var form=new EditorWindow(task==null?"Новая задача":"Изменить задачу","Запиши следующий конкретный шаг.") {Owner=this};
        var title=form.Input("Название задачи",task?.Title??"",160);
        var project=ProjectSelector(false,task?.ProjectId??projectFilter);project.MaxWidth=double.PositiveInfinity;project.Margin=new(0);form.Field("Проект",project);
        var status=new ComboBox {ItemsSource=Statuses,SelectedIndex=(int)(task?.Status??WorkStatus.Planned)};
        var priority=new ComboBox {ItemsSource=Priorities,SelectedIndex=(int)(task?.Priority??Priority.Normal)};
        form.Field("Статус",status);form.Field("Приоритет",priority);
        var due=new DatePicker {SelectedDate=task?.DueDate?.ToDateTime(TimeOnly.MinValue),Language=System.Windows.Markup.XmlLanguage.GetLanguage("ru-RU")};
        due.DateValidationError+=(_,_)=>form.ShowError("Введите дату в формате дд.мм.гггг.");form.Field("Срок · необязательно",due);
        var clear=Btn("Убрать срок",()=>{due.SelectedDate=null;due.Text="";});clear.HorizontalAlignment=HorizontalAlignment.Left;clear.Padding=new(8,4,8,4);form.Body.Children.Add(clear);
        var notes=form.Input("Описание",task?.Notes??"",5000);notes.AcceptsReturn=true;notes.TextWrapping=TextWrapping.Wrap;notes.Height=85;notes.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        if(task!=null)
        {
            var archive=Btn(task.Archived?"Вернуть из архива":"Отправить в архив",()=>{
                if(draft?.TaskId==task.Id)throw new ArgumentException("Сначала завершите сессию этой задачи.");
                store.Archive(task.Id,!task.Archived);form.DialogResult=true;
            });archive.HorizontalAlignment=HorizontalAlignment.Left;form.Body.Children.Add(archive);
        }
        form.OnSave=()=>{
            if(!string.IsNullOrWhiteSpace(due.Text)&&!DateTime.TryParse(due.Text,Ru,DateTimeStyles.None,out _))throw new ArgumentException("Введите корректную дату.");
            store.SaveTask(task?.Id??0,(long)project.SelectedValue,title.Text,notes.Text,(WorkStatus)status.SelectedIndex,(Priority)priority.SelectedIndex,due.SelectedDate is DateTime d?DateOnly.FromDateTime(d):null);
        };
        if(form.ShowDialog()==true)Changed("Задача сохранена.");
    }
    // Render the actual WPF views offscreen against a disposable test database.
    public void VerifyUi(string folder)
    {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FOCUSDESK_DATA")))throw new InvalidOperationException("UI verification requires an explicit disposable FOCUSDESK_DATA directory.");
        Directory.CreateDirectory(folder);store.SeedExamples();Reload();
        Navigate("focus");duration!.SelectedItem=15;
        if(timeDisplay!.Text!="15:00")throw new Exception("Duration selection did not update the timer.");
        timerToggle!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(!clock.Running||store.Draft()==null)throw new Exception("Start button did not persist timer.");
        Thread.Sleep(1100);
        timerToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(clock.Running||store.Draft()!.ElapsedSeconds<1)throw new Exception("Pause button did not checkpoint timer.");
        FinishEarly();if(store.Draft()!=null||store.Sessions().Count==0)throw new Exception("Timer completion did not save session.");
        foreach(string page in new[]{"today","tasks","board","projects","focus","stats","settings"})
        {
            Navigate(page);Width=1280;Height=850;
            var root=(FrameworkElement)Content;root.Measure(new Size(1280,812));root.Arrange(new Rect(0,0,1280,812));root.UpdateLayout();
            var image=new RenderTargetBitmap(1280,812,96,96,PixelFormats.Pbgra32);image.Render(root);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var output=File.Create(Path.Combine(folder,page+".png"));encoder.Save(output);
        }
        ticker.Stop();File.WriteAllText(Path.Combine(folder,"ui-check.txt"),"Rendered 7 WPF views successfully.");
    }
}

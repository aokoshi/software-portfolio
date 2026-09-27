using Microsoft.Data.Sqlite;
using System.Globalization;

namespace FocusDesk.Core;

public sealed class DeskStore
{
    public string DatabasePath { get; }
    public static readonly string[] Colors = ["#7160D5", "#2E8B77", "#CE8743", "#4F80C4", "#B76B8B"];
    public DeskStore(string path)
    {
        DatabasePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
    }
    SqliteConnection Open(string? path = null, bool readOnly = false)
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = path ?? DatabasePath, Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true, Pooling = false, DefaultTimeout = 5 }.ToString());
        c.Open(); return c;
    }
    static SqliteCommand Command(SqliteConnection c, string sql, params object?[] args)
    {
        var cmd = c.CreateCommand(); cmd.CommandText = sql;
        for (int i = 0; i < args.Length; i++) cmd.Parameters.AddWithValue("$" + i, args[i] ?? DBNull.Value);
        return cmd;
    }
    static void Execute(SqliteConnection c, string sql, params object?[] args) { using var cmd = Command(c, sql, args); cmd.ExecuteNonQuery(); }
    public void Initialize()
    {
        using var c = Open();
        using var version = Command(c, "PRAGMA user_version");
        if (Convert.ToInt32(version.ExecuteScalar()) > 1) throw new InvalidDataException("Эта база создана более новой версией FocusDesk.");
        Execute(c, """
            CREATE TABLE IF NOT EXISTS projects(id INTEGER PRIMARY KEY, name TEXT NOT NULL UNIQUE, color TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS tasks(id INTEGER PRIMARY KEY, project_id INTEGER NOT NULL REFERENCES projects(id), title TEXT NOT NULL,
              notes TEXT NOT NULL DEFAULT '', status INTEGER NOT NULL CHECK(status BETWEEN 0 AND 2), priority INTEGER NOT NULL CHECK(priority BETWEEN 0 AND 2),
              due_date TEXT, created_at TEXT NOT NULL, completed_at TEXT, archived INTEGER NOT NULL DEFAULT 0 CHECK(archived IN (0,1)));
            CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY, task_id INTEGER REFERENCES tasks(id), task_title TEXT NOT NULL,
              started_at TEXT NOT NULL, ended_at TEXT NOT NULL, seconds INTEGER NOT NULL CHECK(seconds BETWEEN 1 AND 7200), completed INTEGER NOT NULL CHECK(completed IN (0,1)));
            CREATE TABLE IF NOT EXISTS timer_draft(singleton INTEGER PRIMARY KEY CHECK(singleton=1), id TEXT NOT NULL, task_id INTEGER REFERENCES tasks(id),
              task_title TEXT NOT NULL, started_at TEXT NOT NULL, target INTEGER NOT NULL, elapsed INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_tasks_project ON tasks(project_id, archived, status);
            PRAGMA user_version = 1;
            """);
    }
    static string Clean(string value, int max, string field, bool required = true)
    {
        value = value.Trim();
        if ((required && value.Length == 0) || value.Length > max) throw new ArgumentException($"{field}: {(required ? "от 1 до" : "не более")} {max} символов.");
        return value;
    }
    public long SaveProject(long id, string name, string color)
    {
        name = Clean(name, 60, "Название проекта");
        if (!Colors.Contains(color)) throw new ArgumentException("Выберите цвет из палитры.");
        using var c = Open();
        try
        {
            if (id == 0) { Execute(c, "INSERT INTO projects(name,color) VALUES($0,$1)", name, color); using var q = Command(c, "SELECT last_insert_rowid()"); return (long)q.ExecuteScalar()!; }
            using var cmd = Command(c, "UPDATE projects SET name=$0,color=$1 WHERE id=$2", name, color, id);
            if (cmd.ExecuteNonQuery() != 1) throw new ArgumentException("Проект не найден."); return id;
        }
        catch (SqliteException e) when (e.SqliteExtendedErrorCode == 2067) { throw new ArgumentException("Проект с таким названием уже существует."); }
    }
    public List<Project> Projects()
    {
        using var c = Open(); using var cmd = Command(c, "SELECT id,name,color FROM projects ORDER BY id"); using var r = cmd.ExecuteReader();
        var list = new List<Project>(); while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2))); return list;
    }
    public long SaveTask(long id, long project, string title, string notes, WorkStatus status, Priority priority, DateOnly? due)
    {
        title = Clean(title, 160, "Название задачи"); notes = Clean(notes, 5000, "Описание", false);
        if (!Enum.IsDefined(status) || !Enum.IsDefined(priority)) throw new ArgumentException("Некорректный статус или приоритет.");
        using var c = Open(); using var tx = c.BeginTransaction();
        using var exists = Command(c,"SELECT COUNT(*) FROM projects WHERE id=$0",project);
        if ((long)exists.ExecuteScalar()! != 1) throw new ArgumentException("Выберите существующий проект.");
        string now = DateTimeOffset.UtcNow.ToString("O");
        if (id == 0)
        {
            Execute(c,"INSERT INTO tasks(project_id,title,notes,status,priority,due_date,created_at,completed_at) VALUES($0,$1,$2,$3,$4,$5,$6,$7)",
                project,title,notes,(int)status,(int)priority,due?.ToString("yyyy-MM-dd"),now,status==WorkStatus.Done ? now : null);
            using var q = Command(c,"SELECT last_insert_rowid()"); id = (long)q.ExecuteScalar()!;
        }
        else
        {
            using var cmd = Command(c,"""
                UPDATE tasks SET project_id=$0,title=$1,notes=$2,status=$3,priority=$4,due_date=$5,
                  completed_at=CASE WHEN $3=2 THEN COALESCE(completed_at,$6) ELSE NULL END WHERE id=$7
                """, project,title,notes,(int)status,(int)priority,due?.ToString("yyyy-MM-dd"),now,id);
            if(cmd.ExecuteNonQuery()!=1) throw new ArgumentException("Задача не найдена.");
        }
        tx.Commit(); return id;
    }
    public List<WorkItem> Tasks()
    {
        using var c = Open(); using var cmd = Command(c,"SELECT id,project_id,title,notes,status,priority,due_date,created_at,completed_at,archived FROM tasks ORDER BY id DESC"); using var r = cmd.ExecuteReader();
        var list=new List<WorkItem>();
        while(r.Read()) list.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetString(3),(WorkStatus)r.GetInt32(4),(Priority)r.GetInt32(5),
            r.IsDBNull(6)?null:DateOnly.ParseExact(r.GetString(6),"yyyy-MM-dd",CultureInfo.InvariantCulture),DateTimeOffset.Parse(r.GetString(7)),
            r.IsDBNull(8)?null:DateTimeOffset.Parse(r.GetString(8)),r.GetInt32(9)==1)); return list;
    }
    public void Archive(long id, bool archived) { using var c=Open(); Execute(c,"UPDATE tasks SET archived=$0 WHERE id=$1",archived?1:0,id); }
    public void Move(WorkItem task, WorkStatus status) => SaveTask(task.Id,task.ProjectId,task.Title,task.Notes,status,task.Priority,task.DueDate);
    public List<FocusSession> Sessions()
    {
        using var c=Open(); using var cmd=Command(c,"SELECT id,task_id,task_title,started_at,ended_at,seconds,completed FROM sessions ORDER BY ended_at DESC"); using var r=cmd.ExecuteReader();
        var list=new List<FocusSession>(); while(r.Read()) list.Add(new(r.GetString(0),r.IsDBNull(1)?null:r.GetInt64(1),r.GetString(2),DateTimeOffset.Parse(r.GetString(3)),DateTimeOffset.Parse(r.GetString(4)),r.GetInt32(5),r.GetInt32(6)==1)); return list;
    }
    public void SaveSession(FocusSession s)
    {
        if (s.Seconds is < 1 or > 7200 || s.EndedAt < s.StartedAt || !Guid.TryParse(s.Id, out _)) throw new ArgumentException("Некорректная сессия.");
        using var c=Open(); using var tx=c.BeginTransaction();
        Execute(c,"INSERT INTO sessions(id,task_id,task_title,started_at,ended_at,seconds,completed) VALUES($0,$1,$2,$3,$4,$5,$6) ON CONFLICT(id) DO NOTHING",
            s.Id,s.TaskId,Clean(s.TaskTitle,160,"Задача"),s.StartedAt.ToString("O"),s.EndedAt.ToString("O"),s.Seconds,s.Completed?1:0);
        Execute(c,"DELETE FROM timer_draft WHERE id=$0",s.Id); tx.Commit();
    }
    public void SaveDraft(TimerDraft d)
    {
        if(d.TargetSeconds is < 60 or > 7200 || d.ElapsedSeconds<0 || d.ElapsedSeconds>d.TargetSeconds) throw new ArgumentException("Некорректный таймер.");
        using var c=Open(); Execute(c,"""
            INSERT INTO timer_draft(singleton,id,task_id,task_title,started_at,target,elapsed) VALUES(1,$0,$1,$2,$3,$4,$5)
            ON CONFLICT(singleton) DO UPDATE SET id=excluded.id,task_id=excluded.task_id,task_title=excluded.task_title,started_at=excluded.started_at,target=excluded.target,elapsed=excluded.elapsed
            """,d.Id,d.TaskId,d.TaskTitle,d.StartedAt.ToString("O"),d.TargetSeconds,d.ElapsedSeconds);
    }
    public TimerDraft? Draft()
    {
        using var c=Open(); using var cmd=Command(c,"SELECT id,task_id,task_title,started_at,target,elapsed FROM timer_draft WHERE singleton=1"); using var r=cmd.ExecuteReader();
        return r.Read()?new(r.GetString(0),r.IsDBNull(1)?null:r.GetInt64(1),r.GetString(2),DateTimeOffset.Parse(r.GetString(3)),r.GetInt32(4),r.GetInt32(5)):null;
    }
    public void ClearDraft() { using var c=Open(); Execute(c,"DELETE FROM timer_draft"); }
    public void Backup(string destination)
    {
        if(string.Equals(Path.GetFullPath(destination),DatabasePath,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Выберите другой файл для копии.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        using var source=Open(); using var target=Open(destination); source.BackupDatabase(target);
    }
    public string Restore(string source)
    {
        source=Path.GetFullPath(source);
        if(string.Equals(source,DatabasePath,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Этот файл уже используется.");
        using var candidate=Open(source,true);
        using var integrity=Command(candidate,"PRAGMA integrity_check"); if((string?)integrity.ExecuteScalar()!="ok") throw new InvalidDataException("Копия повреждена.");
        using var version=Command(candidate,"PRAGMA user_version"); if(Convert.ToInt32(version.ExecuteScalar())!=1) throw new InvalidDataException("Неподдерживаемая версия копии.");
        // Validate known entities and their relationships before changing the working database.
        using(var foreign=Command(candidate,"PRAGMA foreign_key_check")) using(var r=foreign.ExecuteReader()) if(r.Read()) throw new InvalidDataException("Нарушены связи в копии.");
        var check=new DeskStore(source); check.Projects(); check.Tasks(); check.Sessions(); check.Draft();
        string safety=Path.Combine(Path.GetDirectoryName(DatabasePath)!,"Backups",$"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
        Backup(safety);
        using var target=Open(); candidate.BackupDatabase(target); Execute(target,"DELETE FROM timer_draft"); return safety;
    }
    public void SeedExamples()
    {
        if(Projects().Count!=0) return;
        long study=SaveProject(0,"Учёба",Colors[0]), work=SaveProject(0,"Портфолио",Colors[1]), life=SaveProject(0,"Личное",Colors[2]);
        var today=DateOnly.FromDateTime(DateTime.Now);
        SaveTask(0,study,"Подготовиться к семинару","Прочитать конспект и выписать вопросы преподавателю.",WorkStatus.Active,Priority.High,today);
        SaveTask(0,work,"Описать StockFlow в портфолио","Добавить скриншоты, стек и краткое описание архитектуры.",WorkStatus.Planned,Priority.Normal,today);
        SaveTask(0,study,"Решить задачи по алгоритмам","Выбрать три задачи и разобрать сложность решений.",WorkStatus.Planned,Priority.High,today.AddDays(1));
        SaveTask(0,work,"Изучить структуру FocusDesk","Посмотреть проекты Core и App. Запустить проверки.",WorkStatus.Planned,Priority.Normal,today.AddDays(2));
        SaveTask(0,life,"Спланировать следующую неделю","Выделить время на учёбу, проекты и отдых.",WorkStatus.Planned,Priority.Low,null);
    }
}

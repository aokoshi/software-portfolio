using FocusDesk.Core;

string folder=Path.Combine(Path.GetTempPath(),"FocusDesk-tests-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);int passed=0;
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
void Reject(Action action,string name){try{action();}catch(ArgumentException){Check(true,name);return;}throw new Exception("FAIL: "+name);}
try
{
 var store=new DeskStore(Path.Combine(folder,"desk.db"));store.Initialize();
 long p=store.SaveProject(0,"Учёба",DeskStore.Colors[0]);
 Reject(()=>store.SaveProject(0,"Учёба",DeskStore.Colors[1]),"Duplicate project rejected");
 Reject(()=>store.SaveProject(0,"  ",DeskStore.Colors[0]),"Empty project rejected");
 Reject(()=>store.SaveTask(0,999,"Task","",WorkStatus.Planned,Priority.Normal,null),"Missing project rejected");
 Reject(()=>store.SaveTask(0,p,"Task","",(WorkStatus)50,Priority.Normal,null),"Invalid status rejected");
 var due=new DateOnly(2026,12,10);
 long id=store.SaveTask(0,p,"Изучить C#","Unicode & <text> ' quotes",WorkStatus.Planned,Priority.High,due);
 var task=store.Tasks().Single();Check(task.DueDate==due&&task.Notes.Contains("' quotes"),"Task text and date round trip");
 var fresh=new DeskStore(store.DatabasePath);fresh.Initialize();Check(fresh.Tasks().Single().Title=="Изучить C#","Persists across connections");
 store.Move(task,WorkStatus.Done);var done=store.Tasks().Single();Check(done.CompletedAt!=null,"Completion timestamp assigned");
 store.SaveTask(done.Id,p,"Updated","",WorkStatus.Done,Priority.Normal,due);Check(store.Tasks().Single().CompletedAt==done.CompletedAt,"Editing completed task retains completion date");
 store.Move(done,WorkStatus.Active);Check(store.Tasks().Single().CompletedAt==null,"Reopening clears completion date");
 store.Archive(id,true);Check(store.Tasks().Single().Archived,"Archive preserves task");store.Archive(id,false);Check(!store.Tasks().Single().Archived,"Archived task can be restored");
 double seconds=0;var clock=new FocusClock(()=>seconds);clock.Configure(60);clock.Start();seconds=12.5;Check(clock.Elapsed==12.5,"Timer measures elapsed time");
 clock.Pause();seconds=80;Check(clock.Elapsed==12.5,"Paused time excluded");clock.Start();seconds=87.5;Check(clock.Elapsed==20,"Resume accumulates work");
 seconds=900;Check(clock.Finished&&clock.Elapsed==60&&clock.Remaining==0,"Delayed tick caps session at target");
 clock.Configure(120,45);Check(!clock.Running&&clock.Elapsed==45,"Recovered timer is paused");
 var now=DateTimeOffset.UtcNow;var sessionId=Guid.NewGuid().ToString();
 store.SaveDraft(new(sessionId,id,"Snapshot title",now,60,30));Check(store.Draft()?.ElapsedSeconds==30,"Checkpoint survives reopening");
 var session=new FocusSession(sessionId,id,"Snapshot title",now,now.AddSeconds(30),30,false);
 store.SaveSession(session);store.SaveSession(session);Check(store.Sessions().Count==1,"Session retry does not double count");Check(store.Draft()==null,"Session and checkpoint completion are atomic");
 Reject(()=>store.SaveSession(session with {Seconds=0}),"Zero duration rejected");
 store.SaveTask(id,p,"Renamed","",WorkStatus.Planned,Priority.Normal,null);Check(store.Sessions().Single().TaskTitle=="Snapshot title","Session title is historical snapshot");
 var backup=Path.Combine(folder,"backup.db");store.Backup(backup);store.SaveProject(0,"Temporary",DeskStore.Colors[1]);
 string safety=store.Restore(backup);Check(store.Projects().Count==1,"Backup restores projects");Check(new DeskStore(safety).Projects().Count==2,"Restore preserves previous data in safety copy");Check(store.Sessions().Single().Seconds==30,"Backup restores focus history");
 Reject(()=>store.Backup(store.DatabasePath),"Cannot overwrite live database with backup");
 File.WriteAllText(Path.Combine(folder,"bad.db"),"not a database");bool bad=false;try{store.Restore(Path.Combine(folder,"bad.db"));}catch{bad=true;}Check(bad&&store.Tasks().Count==1,"Invalid backup leaves current data intact");
 Check(store.Draft()==null,"Restore does not resume stale timer");
 Console.WriteLine($"All {passed} checks passed.");
}
finally
{
 // Only the uniquely named disposable directory owned by this test is removed.
 if(Path.GetFileName(folder).StartsWith("FocusDesk-tests-",StringComparison.Ordinal)&&Path.GetDirectoryName(folder)==Path.TrimEndingDirectorySeparator(Path.GetTempPath()))Directory.Delete(folder,true);
}

using Svetolesye.Core;

int checks=0;
void Check(bool condition,string label){if(!condition)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);checks++;}
GameEngine New(Random? rng=null,string starter="moss"){var e=new GameEngine(rng??new Predictable());e.Start(starter);e.Continue();return e;}
void Finish(GameEngine e){if(e.Screen==Screen.Battle&&e.Fight!.Waiting)e.Continue();while(e.Screen==Screen.Dialogue)e.Continue();}
string dir=Path.Combine(Path.GetTempPath(),"Svetolesye-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
try
{
 var game=New();Check(game.Data!.Team.Count==1&&game.Lead.Hp==game.Lead.MaxHp,"Starter begins healthy");
 var map=game.Map;
 Check(!map.Walkable(0,0)&&!map.Walkable(23,5)&&!map.Walkable(3,11),"Trees, water and houses block movement");
 Check(!map.Walkable(5,13),"Residents occupy their tile");
 var reached=new HashSet<(int,int)>{(7,15)};var queue=new Queue<(int,int)>();queue.Enqueue((7,15));
 while(queue.Count>0){var (x,y)=queue.Dequeue();foreach(var (dx,dy) in new[]{(1,0),(-1,0),(0,1),(0,-1)})if(map.Walkable(x+dx,y+dy)&&reached.Add((x+dx,y+dy)))queue.Enqueue((x+dx,y+dy));}
 Check(map.Residents.All(n=>reached.Any(p=>Math.Abs(p.Item1-n.X)+Math.Abs(p.Item2-n.Y)==1)),"All residents and final encounter are reachable");
 Check(reached.Any(p=>p.Item1<11&&map.Grass(p.Item1,p.Item2))&&reached.Any(p=>p.Item1>11&&map.Grass(p.Item1,p.Item2)),"Both wild habitats are reachable");
 game.Say("Test","Pause");int xBefore=game.Data.X;Check(!game.Move(1,0)&&game.Data.X==xBefore,"Dialogue blocks world movement");game.Continue();
 var travel=game.SafeSnapshot()!;travel.X=3;travel.Y=3;game.Load(travel);
 for(int i=0;i<9&&game.Screen==Screen.World;i++)game.Move(i%2==0?1:-1,0);
 Check(game.Screen==Screen.Battle,"Walking through grass triggers encounters");
 int hp=game.Fight!.Enemy.Hp;game.Attack(false);Check(game.Fight.Enemy.Hp==hp,"Battle introduction must be acknowledged");game.Continue();
 game.Lead.Energy=0;game.Attack(true);Check(game.Fight.Enemy.Hp==hp&&!game.Fight.Waiting,"Empty special attack cannot consume a turn");
 game.Data.Seeds=0;game.Capture();Check(!game.Fight.Waiting,"No seeds cannot trigger capture");
 game.Data.Seeds=8;game.Capture();Check(game.Fight.Outcome==BattleOutcome.Capture&&game.Data.Seeds==7,"Capture consumes one seed and records success");
 game.Continue();Check(game.Data.Team.Count==2&&game.Screen==Screen.Dialogue,"Successful capture adds a companion");game.Continue();game.Continue();Check(game.Data.Team.Count==2,"Repeated confirmation cannot duplicate capture");
 Check(Bestiary.Effect(Element.Ember,Element.Leaf)>1&&Bestiary.Effect(Element.Leaf,Element.Ember)<1,"Element advantages are directional");
 var failed=New(new Predictable(.999));failed.BeginBattle(Creature.Create("drop",3),false);failed.Continue();int before=failed.Lead.Hp;failed.Capture();Check(failed.Data!.Seeds==7&&failed.Lead.Hp<before&&failed.Data.Team.Count==1,"Failed capture consumes seed and allows enemy turn");
 failed.Continue();failed.Run();Finish(failed);Check(failed.Screen==Screen.World&&failed.Fight==null,"Wild escape always returns to world");
 var healed=New();healed.Lead.Hp=5;healed.Data!.Tonics=0;healed.Data.Seeds=0;healed.HealAtVillage();Check(healed.Lead.Hp==healed.Lead.MaxHp&&healed.Data.Tonics==3&&healed.Data.Seeds==8,"Village healing restores resources");
 var potion=New();potion.BeginBattle(Creature.Create("drop",3),false);potion.Continue();potion.Tonic();Check(potion.Data!.Tonics==3&&!potion.Fight!.Waiting,"Full health does not waste tonic");
 potion.Lead.Hp=3;potion.Tonic();Check(potion.Data.Tonics==2&&potion.Lead.Hp>3&&potion.Fight!.Waiting,"Tonic heals and consumes a turn");
 var loss=New();loss.BeginBattle(Creature.Create("keeper",20),false);loss.Continue();loss.Lead.Hp=1;loss.Attack(false);Check(loss.Fight!.Outcome==BattleOutcome.Loss,"Last faint ends encounter");Finish(loss);Check(loss.Data!.X==7&&loss.Data.Y==15&&loss.Lead.Hp==loss.Lead.MaxHp,"Defeat recovers at village without soft lock");
 var switching=New();switching.Data!.Team.Add(Creature.Create("spark",5));switching.BeginBattle(Creature.Create("drop",3),false);switching.Continue();switching.Swap();Check(switching.Data.Lead==1&&switching.Fight!.Waiting,"Switching companion consumes a turn");
 var checkpoint=New();checkpoint.BeginBattle(Creature.Create("spark",5),false);checkpoint.Continue();checkpoint.Attack(false);Check(checkpoint.SafeSnapshot()!.Team[0].Hp==checkpoint.Lead.MaxHp,"Save during battle uses pre-encounter checkpoint");
 var level=New();level.Lead.Experience=level.Lead.NextLevel-1;level.BeginBattle(Creature.Create("drop",3),false);level.Continue();level.Fight!.Enemy.Hp=1;level.Attack(false);Finish(level);Check(level.Lead.Level==6&&level.Lead.Experience<level.Lead.NextLevel,"Victory grants experience and levels correctly");
 var gate=New();var near=gate.SafeSnapshot()!;near.X=18;near.Y=4;gate.Load(near);gate.Interact();Check(gate.Screen==Screen.Dialogue&&gate.Fight==null,"Guardian requires three distinct species");
 gate.Continue();gate.Data!.Team.Add(Creature.Create("spark",5));gate.Data.Team.Add(Creature.Create("moth",5));foreach(var c in gate.Data.Team)gate.Data.Seen.Add(c.SpeciesId);gate.Interact();Check(gate.Fight?.Boss==true,"Three species unlock final encounter");gate.Continue();int seeds=gate.Data.Seeds;gate.Capture();gate.Run();Check(gate.Data.Seeds==seeds&&!gate.Fight!.Waiting,"Guardian cannot be captured or escaped");
 var lastHealthy=New();lastHealthy.Data!.Team.Add(Creature.Create("drop",4));lastHealthy.Data.Team[1].Hp=0;lastHealthy.Open(Screen.Team);lastHealthy.Release(0);Check(lastHealthy.Data.Team.Count==2,"Cannot release last healthy companion");lastHealthy.Release(1);Check(lastHealthy.Data.Team.Count==1,"Can release extra companion");lastHealthy.Release(0);Check(lastHealthy.Data.Team.Count==1,"Cannot release final companion");
 var store=new SaveStore(Path.Combine(dir,"journey.json"));store.Save(level.SafeSnapshot()!);var loaded=store.Load();Check(loaded.Data.Team[0].Level==6&&!loaded.Recovered,"Save round trip retains progress");
 level.Data!.Seeds=6;store.Save(level.SafeSnapshot()!);File.WriteAllText(store.FilePath,"broken");loaded=store.Load();Check(loaded.Recovered&&loaded.Data.Seeds==8,"Corrupt save recovers previous valid backup");store.Save(loaded.Data);Check(!store.Load().Recovered,"Recovered journey can be saved normally");
 var invalid=level.SafeSnapshot()!;invalid.X=0;bool rejected=false;try{SaveStore.Validate(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"Impossible saved position is rejected");
 // Play the final battle through its public turn API with a sensible three-companion team.
 var story=New(new Random(42),"spark");story.Data!.Team.Add(Creature.Create("moss",5));story.Data.Team.Add(Creature.Create("moth",5));foreach(var c in story.Data.Team)story.Data.Seen.Add(c.SpeciesId);
 bool savedVictory=false;story.SaveRequested+=d=>savedVictory|=d.KeeperDefeated;
 story.BeginBattle(Creature.Create("keeper",8),true);story.Continue();int turns=0;
 while(story.Fight!=null&&turns++<100)
 {
     if(story.Fight.Waiting){story.Continue();continue;}
     if(story.Lead.Hp<20&&story.Data.Tonics>0)story.Tonic();else story.Attack(story.Lead.Energy>0);
 }
 Check(story.Data.KeeperDefeated&&savedVictory,"Final encounter can be won and victory is saved");while(story.Screen==Screen.Dialogue)story.Continue();Check(story.Screen==Screen.Victory,"Final dialogue leads to victory screen");story.Open(Screen.World);Check(story.Screen==Screen.World,"Exploration continues after victory");
 foreach(var region in Enum.GetValues<Region>())
 {
     var world=new World(region);var visited=new HashSet<(int,int)>{world.Camp};var pending=new Queue<(int,int)>();pending.Enqueue(world.Camp);
     while(pending.Count>0){var (x,y)=pending.Dequeue();foreach(var (dx,dy) in new[]{(1,0),(-1,0),(0,1),(0,-1)})if(world.Walkable(x+dx,y+dy)&&visited.Add((x+dx,y+dy)))pending.Enqueue((x+dx,y+dy));}
     Check(world.Residents.All(n=>visited.Any(p=>Math.Abs(p.Item1-n.X)+Math.Abs(p.Item2-n.Y)==1)),region+" residents reachable");
     Check(world.Portals.All(p=>visited.Contains((p.X,p.Y))),region+" portals reachable");
 }
 var evolving=New();evolving.Open(Screen.Team);evolving.Evolve(0);Check(evolving.Lead.SpeciesId=="moss","Evolution locked before level 8");
 evolving.Lead.GainExperience(110);evolving.Lead.Hp=9;int oldMax=evolving.Lead.MaxHp,oldXp=evolving.Lead.Experience;
 evolving.Evolve(0);Check(evolving.Screen==Screen.Evolution&&evolving.Lead.SpeciesId=="verdant","Evolution opens new form screen");
 Check(evolving.Lead.Hp==9+evolving.Lead.MaxHp-oldMax&&evolving.Lead.Experience==oldXp,"Evolution preserves XP and missing HP");
 Check(evolving.Data!.Seen.Contains("verdant")&&evolving.DistinctSpecies==1&&!evolving.Lead.CanEvolve,"Evolved family counted once");
 var faint=Creature.Create("drop",8);faint.Hp=0;faint.Evolve();faint.GainExperience(200);Check(faint.Hp==0,"Evolution and levels do not revive fainted creatures");
 var capped=Creature.Create("spark",29);capped.GainExperience(10000);Check(capped.Level==30&&capped.Experience==0,"Level cap clears excess experience");
 var xpTeam=New();xpTeam.Data!.Team.Add(Creature.Create("drop",5));xpTeam.BeginBattle(Creature.Create("moth",3),false);xpTeam.Continue();xpTeam.Fight!.Enemy.Hp=1;xpTeam.Attack(false);Finish(xpTeam);
 Check(xpTeam.Lead.Experience==21&&xpTeam.Data.Team[1].Experience==10,"Bench receives half victory XP");
 var migrate=New().SafeSnapshot()!;migrate.Version=1;migrate.KeeperDefeated=true;migrate.Team.Add(Creature.Create("drop",6));
 string legacyPath=Path.Combine(dir,"legacy.json");string raw=System.Text.Json.JsonSerializer.Serialize(migrate);File.WriteAllText(legacyPath,raw);
 var legacyStore=new SaveStore(legacyPath);var migrated=legacyStore.Load().Data;
 Check(migrated.Version==2&&migrated.KeeperDefeated&&migrated.Team.Count==2&&migrated.Region==Region.Valley,"Version 1 progress migrates");
 Check(File.ReadAllText(legacyPath)==raw,"Reading legacy save does not overwrite it");
 legacyStore.Save(migrated);Check(File.ReadAllText(legacyPath+".v1.bak")==raw,"Permanent original legacy backup retained");
 var crossing=New();crossing.Data!.X=23;crossing.Data.Y=15;crossing.Move(1,0);Check(crossing.Data.Region==Region.Valley,"Coast gate locked before guardian");Finish(crossing);
 crossing.Data.KeeperDefeated=true;crossing.Move(1,0);Check(crossing.Data.Region==Region.Coast&&crossing.Map.Region==Region.Coast,"Unlocked gate changes region");
 crossing.Move(-1,0);Check(crossing.Data.Region==Region.Valley,"Return portal works");
 var coast=New().SafeSnapshot()!;coast.KeeperDefeated=true;coast.Region=Region.Coast;coast.X=5;coast.Y=5;
 var chapters=New();chapters.Load(coast);chapters.Interact();Finish(chapters);chapters.Interact();Finish(chapters);Check(chapters.Data!.Beacons.Count==1,"Beacon activation is idempotent");
 foreach(var region in new[]{Region.Coast,Region.Summit})
 {
     var snapshot=chapters.SafeSnapshot()!;snapshot.Region=region;snapshot.KeeperDefeated=true;snapshot.TideDefeated=region==Region.Summit;snapshot.X=new World(region).Camp.X;snapshot.Y=new World(region).Camp.Y;
     snapshot.Team=[Creature.Create("verdant",14),Creature.Create("tide",14),Creature.Create("boulder",14)];snapshot.Lead=0;chapters.Load(snapshot);
     chapters.BeginBattle(Creature.Create(region==Region.Coast?"tidekeeper":"astral",region==Region.Coast?12:16),true);chapters.Continue();int moves=0;
     while(chapters.Fight!=null&&moves++<150){if(chapters.Fight.Waiting)chapters.Continue();else if(chapters.Lead.Hp<35&&chapters.Data!.Tonics>0)chapters.Tonic();else chapters.Attack(chapters.Lead.Energy>0);}
     Finish(chapters);Check(region==Region.Coast?chapters.Data!.TideDefeated:chapters.Data!.SummitDefeated,region+" guardian winnable");
     SaveStore.Validate(chapters.SafeSnapshot()!);
 }
 var quest=New();var q=quest.SafeSnapshot()!;q.KeeperDefeated=true;q.Region=Region.Coast;q.X=19;q.Y=4;quest.Load(q);quest.Interact();Check(quest.Fight==null&&quest.Screen==Screen.Dialogue,"Coast boss requires three beacons");Finish(quest);
 quest.Data!.Beacons.UnionWith(["beacon-west","beacon-south","beacon-east"]);quest.Interact();Check(quest.Fight?.Enemy.SpeciesId=="tidekeeper","Three beacons unlock coast boss");
 q=New().SafeSnapshot()!;q.KeeperDefeated=true;q.TideDefeated=true;q.Region=Region.Summit;q.X=19;q.Y=4;quest.Load(q);quest.Interact();Check(quest.Fight==null,"Summit boss requires trials");Finish(quest);
 quest.BeginBattle(Creature.Create("luna",13),true,"trial-air");quest.Continue();quest.Fight!.Enemy.Hp=1;quest.Attack(false);Finish(quest);
 Check(quest.Data!.Trials.SetEquals(["trial-air"])&&!quest.Data.SummitDefeated,"Trial victory records only its own progress");
 quest.Data.Trials.Add("trial-stone");quest.Interact();Check(quest.Fight?.Enemy.SpeciesId=="astral","Two trials unlock final boss"); var sound=new AudioSettings{Music=140,Effects=-4,Ambience=25};sound.Clamp();Check(sound.Music==100&&sound.Effects==0,"Audio volume bounds enforced");sound.Save(Path.Combine(dir,"audio.json"));Check(AudioSettings.Load(Path.Combine(dir,"audio.json")).Ambience==25,"Audio settings persist");
 string audioRoot=Path.Combine(Directory.GetCurrentDirectory(),"src","Svetolesye.Game","Assets","Audio");
 foreach(string file in Directory.GetFiles(audioRoot,"*.wav"))
 {
     using var reader=new BinaryReader(File.OpenRead(file));var bytes=reader.ReadBytes((int)reader.BaseStream.Length);
     bool valid=System.Text.Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(bytes,8,4)=="WAVE"&&BitConverter.ToInt32(bytes,24)==22050&&BitConverter.ToInt16(bytes,22)==2;
     int peak=0;long energy=0;for(int i=44;i<bytes.Length;i+=2){int sample=BitConverter.ToInt16(bytes,i);peak=Math.Max(peak,Math.Abs(sample));energy+=(long)sample*sample;}
     Check(valid&&peak<32767&&energy>0,Path.GetFileName(file)+" valid stereo PCM without clipping");
 } Console.WriteLine($"All {checks} checks passed.");
}
finally
{
 if(Path.GetFileName(dir).StartsWith("Svetolesye-test-",StringComparison.Ordinal)&&Path.GetDirectoryName(dir)==Path.TrimEndingDirectorySeparator(Path.GetTempPath()))Directory.Delete(dir,true);
}
sealed class Predictable(double chance=0):Random
{
 public override double NextDouble()=>chance;
 public override int Next(int maxValue)=>0;
 public override int Next(int minValue,int maxValue)=>minValue;
}

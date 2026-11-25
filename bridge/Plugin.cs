using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace SongbookSync;
public sealed class Plugin : IDalamudPlugin {
#if TRAMATIC_COMPANION
 const string CommandName="/tramaticsync";
 const string DisplayName="Siren's call Song Request Book";
 string Endpoint="https://tramatic-songbook.affectionategecko4.chatgpt.site/api/midibard";
#else
 const string CommandName="/songbooksync";
 const string DisplayName="Siren's call Song Request Book";
 string Endpoint="";
#endif
 readonly IDalamudPluginInterface pi;
 readonly IFramework framework;
 readonly ICommandManager commands;
 readonly IChatGui chat;
 readonly IClientState client;
 readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(10)};
 readonly CancellationTokenSource stop=new();
 readonly PlaybackTracker tracker=new();
 readonly HashSet<string> announced=new();
 readonly string configPath;
 string remoteEventId="";
 string token="",status="Not paired. Use the connection command from your songbook.",eventShowId="";
 bool announcements=true;
 string playbackSource="Not detected";
 DateTime nextPoll=DateTime.MinValue;
 volatile bool busy;
 public Plugin(IDalamudPluginInterface pi,IFramework framework,ICommandManager commands,IChatGui chat,IClientState client) {
  this.pi=pi;this.framework=framework;this.commands=commands;this.chat=chat;this.client=client;
  configPath=Path.Combine(pi.GetPluginConfigDirectory(),"connection.json");
  try{var config=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(configPath));token=config?.GetValueOrDefault("token")??"";Endpoint=config?.GetValueOrDefault("endpoint")??Endpoint;announcements=config?.GetValueOrDefault("announcements")!="false";}catch{}
  Baseline();
  commands.AddHandler(CommandName,new CommandInfo(Command){HelpMessage="Pair with "+CommandName+" HTTPS_URL TOKEN; status; disconnect; announcements on|off"});
  framework.Update+=Update;
 }
 void Save(){Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);File.WriteAllText(configPath,JsonSerializer.Serialize(new Dictionary<string,string>{{"token",token},{"endpoint",Endpoint},{"announcements",announcements?"true":"false"}}));}
 MidiBardSnapshot? Playback(){
  commands.Commands.TryGetValue("/midibard",out var command);
  if(command==null)commands.Commands.TryGetValue("/mbard",out command);
  var snapshot=MidiBardReader.Read(command?.Handler);
  playbackSource=snapshot?.Source??"Not detected. Enable MidiBard 2 or MidiBard 2 Alpha.";
  return snapshot;
 }
 void Baseline(){try{var snapshot=Playback();tracker.Baseline(snapshot?.Playing==true?snapshot.Playback:null);}catch{playbackSource="Playback interface could not be read.";tracker.Baseline(null);}}
 void Command(string command,string args){
  args=args.Trim();
  if(args=="status"||args==""){try{Playback();}catch{playbackSource="Playback interface could not be read.";}chat.Print("["+DisplayName+" v1.3.4] "+status+" Say announcements: "+(announcements?"on":"off")+" Playback source: "+playbackSource);return;}
  if(args=="announcements on"||args=="announcements off"){announcements=args.EndsWith(" on");Save();chat.Print("["+DisplayName+"] Say announcements "+(announcements?"on.":"off."));return;}
  if(args=="disconnect"){token="";File.Delete(configPath);Baseline();status="Disconnected.";chat.Print("["+DisplayName+"] "+status);return;}
#if TRAMATIC_COMPANION
  if(args.Length==64&&args.All(Uri.IsHexDigit))args=new Uri(Endpoint).GetLeftPart(UriPartial.Authority)+" "+args;
#endif
  var pair=args.Split(' ',StringSplitOptions.RemoveEmptyEntries);
  if(pair.Length!=2||!Uri.TryCreate(pair[0],UriKind.Absolute,out var target)||target.Scheme!="https"||!string.IsNullOrEmpty(target.UserInfo)||pair[1].Length!=64||!pair[1].All(Uri.IsHexDigit)){chat.Print("["+DisplayName+"] Copy the full connection command from your owner songbook.");return;}
  Endpoint=new Uri(target.GetLeftPart(UriPartial.Authority)+"/api/midibard").AbsoluteUri;token=pair[1];Save();Baseline();
  status="Paired locally. Checking songbook connection…";chat.Print("["+DisplayName+"] "+status);_=Send(null);
 }
 void Update(IFramework f){
  if(DateTime.UtcNow<nextPoll||busy||token.Length==0||Endpoint.Length==0)return;
  nextPoll=DateTime.UtcNow.AddSeconds(2);
  try{
   var snapshot=Playback();
   var update=tracker.Observe(snapshot?.Playback,snapshot?.Playing==true,snapshot?.Seconds,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
   if(snapshot==null&&update==null){status="Waiting for MidiBard 2. Enable MidiBard 2 or MidiBard 2 Alpha.";return;}
   if(update!=null){if(update.Playing&&remoteEventId!=update.EventId)update=update with{Action="start"};_=Send(update);}
  }catch{status="Unable to read MidiBard 2 playback. Use "+CommandName+" status after reloading both plugins.";}
 }
 async Task Send(PlaybackUpdate? update){
  busy=true;var activeToken=token;
  try{
   if(update==null||update.Action=="start"){
    using var get=new HttpRequestMessage(HttpMethod.Get,Endpoint);get.Headers.Authorization=new AuthenticationHeaderValue("Bearer",activeToken);
    using var response=await http.SendAsync(get,stop.Token);
    if(!response.IsSuccessStatusCode){status=response.StatusCode==System.Net.HttpStatusCode.Unauthorized?"The saved key was not accepted. Check the connection in your songbook.":"Songbook unavailable. Your key is kept; use Refresh connection and try the next song.";return;}
    using var data=JsonDocument.Parse(await response.Content.ReadAsStringAsync(stop.Token));
    if(update==null){status="Connected. Waiting for the next song to start.";return;}
    eventShowId=data.RootElement.GetProperty("showId").GetString()??"";
   }
   using var request=new HttpRequestMessage(HttpMethod.Post,Endpoint){Content=JsonContent.Create(new{action=update.Action,file=update.File,eventId=update.EventId,showId=eventShowId,playing=update.Playing,startedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()})};
   request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",activeToken);
   using var posted=await http.SendAsync(request,stop.Token);
   if(!posted.IsSuccessStatusCode){status="Song update failed or show changed. Start the next song, or check your connection.";return;}
   using var result=JsonDocument.Parse(await posted.Content.ReadAsStringAsync(stop.Token));
   if(token==activeToken)remoteEventId=update.EventId;
   var matched=result.RootElement.GetProperty("matched").GetBoolean();
   status=update.Playing?(matched?"Playing now: "+update.File:"Song needs matching. Open MidiBard Sync in your songbook."):"Playback paused or stopped.";
   if(update.Playing&&result.RootElement.TryGetProperty("announcement",out var message)&&message.ValueKind==JsonValueKind.String){
    var text=message.GetString();if(!string.IsNullOrWhiteSpace(text))await framework.RunOnFrameworkThread(()=>{
     var current=Playback();
     if(stop.IsCancellationRequested||token!=activeToken||!announcements||!client.IsLoggedIn||announced.Contains(update.EventId)||current?.Playing!=true||!ReferenceEquals(current.Playback,update.Playback))return;
     if(Say(text))announced.Add(update.EventId);
    });
   }
  }catch(OperationCanceledException){if(!stop.IsCancellationRequested)status="Connection timed out. Check your internet connection.";}
   catch{status="Could not update the songbook or say announcement. Use "+CommandName+" status.";}
   finally{busy=false;}
 }
 static unsafe bool Say(string text){
  var ui=UIModule.Instance();if(ui==null)return false;
  using var message=new Utf8String(SayText.Command(text));ui->ProcessChatBoxEntry(&message);return true;
 }
 public void Dispose(){framework.Update-=Update;commands.RemoveHandler(CommandName);stop.Cancel();http.Dispose();}
}

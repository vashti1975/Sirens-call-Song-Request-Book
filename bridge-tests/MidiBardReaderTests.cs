using SongbookSync;

internal static class MidiBardReaderTests {
 internal static void Run() {
  var first = new MidiBard.TestPlayback { Seconds = 12, Running = true };
  var plugin = new MidiBard.Plugin { CurrentBardPlayback = first };
  var commands = new MidiBard.PluginCommandManager(plugin);
  Action<string,string> handler = commands.OnMainCommand;
  var snapshot = MidiBardReader.Read(handler)!;
  Require(ReferenceEquals(snapshot.Playback, first) && snapshot.Playing && snapshot.Seconds == 12,
   "Alpha must read the command owner's instance playback and position.");
  Require(PlaybackDetails.FileName(snapshot.Playback!) == "Alpha song.mid", "Alpha filename must reach song matching.");
  var tracker = new PlaybackTracker();
  var started = tracker.Observe(snapshot.Playback, snapshot.Playing, snapshot.Seconds, 0)!;
  Require(started.Action == "start" && started.File == "Alpha song.mid", "Alpha start must create a song event.");
  first.Running = false;
  snapshot = MidiBardReader.Read(handler)!;
  Require(!snapshot.Playing && tracker.Observe(snapshot.Playback, snapshot.Playing, snapshot.Seconds, 2000)?.Playing == false,
   "Alpha pause must clear the live song.");
  first.Running = true;
  snapshot = MidiBardReader.Read(handler)!;
  Require(tracker.Observe(snapshot.Playback, snapshot.Playing, snapshot.Seconds, 4000)?.EventId == started.EventId,
   "Alpha resume must keep its event and avoid another announcement.");
  var next = new MidiBard.TestPlayback { Running = true };
  plugin.CurrentBardPlayback = next;
  snapshot = MidiBardReader.Read(handler)!;
  Require(ReferenceEquals(snapshot.Playback, next), "Alpha must reread CurrentBardPlayback for every song.");
  Require(tracker.Observe(snapshot.Playback, snapshot.Playing, snapshot.Seconds, 6000)?.Action == "start",
   "The next Alpha song must start a new event.");
  plugin.CurrentBardPlayback = null;
  Require(MidiBardReader.Read(handler) is { Playback: null, Playing: false }, "Alpha with no file must be idle.");

  // Both layouts exist in this assembly: the active command owner must win,
  // even while a disabled standard MidiBard class is still loaded.
  MidiBard.MidiBard.CurrentPlayback = first;
  MidiBard.MidiBard.IsPlaying = true;
  Require(MidiBardReader.Read(handler)?.Playback == null, "A stale standard assembly must not mask Alpha.");
  Action<string,string> standard = MidiBard.LegacyCommands.OnCommand;
  snapshot = MidiBardReader.Read(standard)!;
  Require(ReferenceEquals(snapshot.Playback, first) && snapshot.Playing && snapshot.Seconds == 9,
   "Standard MidiBard static playback must stay compatible.");
  Require(MidiBardReader.Read(null) == null, "Disabled/unloaded MidiBard must not reuse old loaded assemblies.");
  var replacement = new MidiBard.PluginCommandManager(new MidiBard.Plugin { CurrentBardPlayback = next });
  Require(ReferenceEquals(MidiBardReader.Read((Action<string,string>)replacement.OnMainCommand)?.Playback, next),
   "Reloading Alpha must use the new registered instance.");
  try { MidiBardReader.Read((Action<string,string>)Unrelated); throw new Exception("Unsupported command owner was accepted."); }
  catch (MissingMemberException) { }
  Console.WriteLine("Passed: Alpha instance discovery, filename/position, start/pause/resume/next song, idle state, standard compatibility, disabled copies and plugin reload.");
 }
 static void Unrelated(string command, string args) { }
 static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}

namespace MidiBard {
 internal sealed class Plugin { internal TestPlayback? CurrentBardPlayback { get; set; } }
 internal sealed class PluginCommandManager(Plugin plugin) {
  private Plugin Plugin { get; } = plugin;
  internal void OnMainCommand(string command, string args) { }
 }
 internal sealed class TestPlayback {
  internal string FilePath => "C:\\Music\\Alpha song.mid";
  public bool IsRunning => Running;
  internal bool Running { get; set; }
  internal double Seconds { get; set; }
  public TimeSpan GetCurrentTimeSpan() => TimeSpan.FromSeconds(Seconds);
 }
 internal static class MidiBard {
  internal static object? CurrentPlayback { get; set; }
  internal static bool IsPlaying { get; set; }
  internal static TimeSpan? CurrentPlaybackTime => TimeSpan.FromSeconds(9);
 }
 internal static class LegacyCommands { internal static void OnCommand(string command, string args) { } }
}

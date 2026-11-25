using System.Reflection;

namespace SongbookSync;

internal sealed record MidiBardSnapshot(object? Playback, bool Playing, double? Seconds, string Source);

internal static class MidiBardReader {
 const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
 const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

 // Follow the registered command's owner, so disabled or unloaded copies are
 // never selected just because their assembly is still present in the process.
 internal static MidiBardSnapshot? Read(Delegate? handler) {
  if (handler == null) return null;
  var commandNamespace = handler.Method.DeclaringType?.Namespace;
  if (commandNamespace != "MidiBard" && commandNamespace?.StartsWith("MidiBard.", StringComparison.Ordinal) != true)
   throw new MissingMemberException("The active command is not owned by MidiBard.");
  var assembly = handler.Method.DeclaringType?.Assembly ?? handler.Method.Module.Assembly;
  var owner = handler.Target;
  var plugin = owner?.GetType().FullName == "MidiBard.Plugin"
   ? owner : owner?.GetType().GetProperty("Plugin", Instance)?.GetValue(owner);
  if (plugin?.GetType().FullName == "MidiBard.Plugin") {
   var property = plugin.GetType().GetProperty("CurrentBardPlayback", Instance)
    ?? throw new MissingMemberException("MidiBard Alpha playback interface changed.");
   var playback = property.GetValue(plugin);
   if (playback == null) return new(null, false, null, Source(plugin.GetType().Assembly));
   var running = playback.GetType().GetProperty("IsRunning", Instance)
    ?? throw new MissingMemberException("MidiBard Alpha playing state is unavailable.");
   var position = playback.GetType().GetMethod("GetCurrentTimeSpan", Instance, Type.EmptyTypes)?.Invoke(playback, null);
   return new(playback, running.GetValue(playback) is true,
    position is TimeSpan span ? span.TotalSeconds : null, Source(plugin.GetType().Assembly));
  }

  // Standard MidiBard 2 exposes static playback properties.
  var legacy = assembly.GetType("MidiBard.MidiBard", false);
  if (legacy == null) throw new MissingMemberException("Unsupported MidiBard playback interface.");
  var current = legacy.GetProperty("CurrentPlayback", Static)
   ?? throw new MissingMemberException("MidiBard playback interface changed.");
  var playing = legacy.GetProperty("IsPlaying", Static)
   ?? throw new MissingMemberException("MidiBard playing state is unavailable.");
  var time = legacy.GetProperty("CurrentPlaybackTime", Static)?.GetValue(null);
  return new(current.GetValue(null), playing.GetValue(null) is true,
   time is TimeSpan seconds ? seconds.TotalSeconds : null, Source(assembly));
 }

 static string Source(Assembly assembly) {
  var name = assembly.GetName();
  var label = name.Name switch {
   "ZuneBardAlpha" => "MidiBard 2 Alpha",
   "ZuneBard" => "MidiBard 2 Beta",
   _ => "MidiBard 2"
  };
  return label + " " + name.Version;
 }
}

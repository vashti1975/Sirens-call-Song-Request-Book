# MidiBard companion

Install **Siren's call Song Request Book** through the normal Dalamud plugin installer using the [installer and migration guide](PLUGIN-INSTALLER.md). The shared repository offers a configurable `SongbookSync` variant and the compatible `TramaticSync` variant for existing installations.

The companion observes MidiBard 2 playback and sends filenames and playing state to a hosted songbook. It does not replace MidiBard or include its binaries or music. Run one companion on the lead bard. Source and the compiled releases use AGPL-3.0; third-party dependencies keep their own licenses.

Version 1.3.4 supports the standard MidiBard 2 static playback layout and the instance playback layout used by Zune's MidiBard 2 Alpha (verified against `ZuneBardAlpha` 1.15.0.171). It follows the active `/midibard` command owner rather than old assemblies left behind after disabling or reloading a plugin. `/tramaticsync status` (or `/songbooksync status`) includes the detected playback source and version. Keep the MidiBard version you intend to use enabled, update the companion, and start a new song. Your saved connection key and song matches remain in use. Live FFXIV playback still needs an in-game check.

## Pair once

For the general companion, copy `/songbooksync HTTPS_URL TOKEN` from your own owner page. Its HTTPS songbook origin is configurable and saved with the private key. For the Tramatic companion, `/tramaticsync TOKEN` continues to use the existing endpoint and saved configuration format. Keep the command and connection.json private.

Saved automatic and manual filename matches persist across shows. A filename can be reset for another owner choice. **Ignore this song** excludes a solo filename; **Allow matching** lets the owner choose a match later. Ignored files do not create announcements or live request-book song labels. Unresolved ignored events are excluded from the one-hour request total.

## Recover with the same key

Use **MidiBard Sync → Setup & connection → Refresh connection** for a stale connection. It keeps the key, matches, ignored files, requests and history. The page waits for an accepted playback update before showing confirmation. Keep MidiBard 2 and the companion enabled, then start a new song if it remains waiting. A lost playback row can be rebuilt only from the latest event in the current show, without recording another play or clearing later requests.

Key replacement is under **Connection key options** and is intended for a missing, revoked or exposed key. The website cannot start a disabled plugin. If the key was revoked or the local configuration deleted, pair again with the matching command.

## Playback and announcements

Matched starts mark the relevant song played and complete only requests already waiting at the start. Live Playing now expires after about 20 seconds without updates. Pause/stop clears the live label, and resume does not repeat an announcement. A replay or new song gets its own event identity.

Optional `/s` announcements include title and artist, plus By Request and any supplied names when the owner’s name switch is enabled. The companion’s `announcements on` / `announcements off` command is saved locally. Only one companion should run to avoid duplicate announcements.

Build and packaging tests run in the Windows release workflow. Actual FFXIV chat delivery and installation migration require a live check in the game; keep the previous local files until that succeeds.

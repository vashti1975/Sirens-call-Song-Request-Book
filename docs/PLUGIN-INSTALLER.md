# Normal Dalamud installation

Add this URL in `/xlsettings` → **Experimental** → **Custom Plugin Repositories**, enable it, and save:

```text
https://raw.githubusercontent.com/vashti1975/Sirens-call-Song-Request-Book/main/pluginmaster.json
```

Then open `/xlplugins` and install one companion on your lead bard. Both variants now display **Siren's call Song Request Book**; existing installations retain their internal identity and saved settings:

| Installer name | Internal name | Use |
| --- | --- | --- |
| Siren's call Song Request Book | SongbookSync | Any hosted copy of the reusable songbook; pair with `/songbooksync HTTPS_URL TOKEN`. |
| Siren's call Song Request Book | TramaticSync | The existing Tramatic songbook; retains `/tramaticsync`, its original endpoint, and saved key format. |

MidiBard 2 must remain enabled. The website is still the owner/listener interface. This custom repository is separate from the official Dalamud plugin list and does not involve Penumbra.

## Switch from Dev Tools

1. Keep a local backup of the existing companion configuration. `connection.json` contains your private key; do not upload it.
2. Disable the development instance and remove its DLL entry from **Dev Plugin Locations**. Keep the files until the replacement has been checked.
3. Install the matching normal version in `/xlplugins`. Do not run both versions together.
4. For the compatible TramaticSync variant, the normal version keeps the `TramaticSync` identity and configuration format. Use `/tramaticsync status` and the owner page’s **Refresh connection** before considering a new key. Do not delete the configuration when moving installations.
5. Play a new song and verify Playing now, request completion, and one `/s` announcement. Check pause/resume and a replay. If configuration is missing, use the existing saved pairing command if you have it; replace the key only when it is no longer available or accepted.

## Build and releases

Both projects use `Dalamud.NET.Sdk/15.0.0`, .NET 10, and version 1.3.4. The Windows workflow downloads pinned official API 15 libraries, tests standard and Alpha playback/announcement logic and installer packaging, and compiles both assemblies. Packaging checks the manifest’s internal name, assembly version and API against its project and only includes the plugin DLL, its generated manifest and AGPL license. No connection settings, keys, songs, private artwork or host libraries are distributed.

After successful compilation on main, the workflow publishes a versioned GitHub release and writes its two-entry `pluginmaster.json` to main. Install/update URLs point to the corresponding versioned ZIPs in that release. Failed builds do not update the feed. Existing release tags are not overwritten: increase both projects’ Version and AssemblyVersion for another release. The repository and release downloads must be public for Dalamud to fetch the feed without authentication.

Local Windows checks, with .NET 10 and `DALAMUD_HOME` pointing to the installed Dalamud libraries:

```powershell
dotnet run --project bridge-tests/test.csproj --configuration Release
dotnet build bridge/SongbookSync.csproj --configuration Release
dotnet build bridge-tramatic/TramaticSync.csproj --configuration Release
python scripts/test-plugin-package.py
python scripts/prepare-release.py --repository vashti1975/Sirens-call-Song-Request-Book
```

Compiled packages and the release feed appear in `plugin-dist`. `public/SongbookSync-source.zip` is an optional source download and is not an installer ZIP. Build and packaging checks cannot verify actual game playback; the first live migration still needs the checks above.

## Connection recovery and solo files

**Refresh connection** preserves the pairing key, matches, ignored files, requests and played history. It waits for the companion’s next accepted playback update and can rebuild the latest recorded session without recording another play. If waiting, keep both plugins enabled and start a new song.

**Ignore this song** remembers an excluded filename across shows, skips matching and announcements, and excludes unresolved ignored plays from the one-hour request total. **Allow matching** restores an owner choice.

References: [custom repository format](https://dalamud.dev/plugin-publishing/custom-repositories/) and [API 15 SDK](https://dalamud.dev/versions/v15/).

Public installer mirror for the same compiled general and Tramatic packages: [https://tramatic-songbook.affectionategecko4.chatgpt.site/pluginmaster.json](https://tramatic-songbook.affectionategecko4.chatgpt.site/pluginmaster.json). This mirror remains usable while GitHub repository visibility is being changed.

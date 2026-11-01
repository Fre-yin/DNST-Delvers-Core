# Dein erstes Delvers-Paket in 30 Minuten

**Deutsch** | [English](CORE-ADDON-SCHNELLSTART.en.md)

Diese Anleitung führt dich vom leeren Ordner bis zu einem eigenen Paket, das im Delvers-Menü als eigener Reiter erscheint. Du brauchst dafür keine Vorkenntnisse in C#. Jeder Schritt sagt, was du tippst und was du danach sehen solltest.

Was das Paket am Ende kann: Es lädt mit dem Spiel, prüft, ob Delvers Core da ist, und zeigt im Delvers-Menü eine eigene Seite mit einem Text und einem Schalter. Von dort aus kannst du weiterbauen.

## 1. Was du brauchst

- Windows 10 oder 11, 64 Bit.
- Dungeon Settlers in der Version DS_B.0.4.29. Bei einer anderen Spielversion bleibt Core aus und meldet das im Log.
- BepInEx 6 für Unity IL2CPP x64 (getestet mit 6.0.0-be.788), installiert im Spielordner. Starte das Spiel einmal mit BepInEx, bis das Hauptmenü erscheint, und beende es wieder. Erst dann gibt es den Ordner `BepInEx\interop`.
- Delvers Core 0.5.0, installiert wie im README beschrieben. Im Spiel steht dann in den Optionen der letzte Reiter „Delvers“.
- Das .NET SDK 6 oder neuer von [dotnet.microsoft.com](https://dotnet.microsoft.com/download). Prüfe die Installation in einer Eingabeaufforderung:

  ```
  dotnet --version
  ```

  Es erscheint eine Versionsnummer, zum Beispiel `8.0.100`. Mit einem neueren SDK als 6 lädt der erste Bau einmal Dateien für .NET 6 aus dem Internet nach; halte dafür eine Verbindung bereit.
- Ein Texteditor. Der Windows-Editor reicht, Visual Studio Code ist bequemer.

In dieser Anleitung liegt das Spiel unter `C:\Spiele\Dungeon Settlers`. Ersetze den Pfad überall durch deinen eigenen Spielordner, also den Ordner, in dem `DungeonSettlers.exe` liegt.

## 2. Den Ordner anlegen

Lege einen leeren Ordner für dein Paket an, zum Beispiel `C:\Mods\MeinPaket`. Du legst darin nur zwei Dateien an.

Wähle einen Namen für dein Paket und eine Paket-ID. Die Paket-ID besteht aus Kleinbuchstaben, Ziffern und Bindestrichen, zum Beispiel `mein-paket`. Sie darf nicht von Core oder einem anderen Paket benutzt werden.

## 3. Die Projektdatei

Lege im Ordner die Datei `MeinPaket.csproj` an und füge ein:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>MeinPaket</AssemblyName>
    <Nullable>disable</Nullable>
    <!-- .NET 6 ist alt, aber das Spiel braucht es. Diese Zeile blendet die Warnung dazu aus. -->
    <CheckEolTargetFramework>false</CheckEolTargetFramework>
    <!-- Dein Spielordner, mit \ am Ende. -->
    <GameDir>C:\Spiele\Dungeon Settlers\</GameDir>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="BepInEx.Core"><HintPath>$(GameDir)BepInEx\core\BepInEx.Core.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="SemanticVersioning"><HintPath>$(GameDir)BepInEx\core\SemanticVersioning.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="BepInEx.Unity.IL2CPP"><HintPath>$(GameDir)BepInEx\core\BepInEx.Unity.IL2CPP.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Il2CppInterop.Runtime"><HintPath>$(GameDir)BepInEx\core\Il2CppInterop.Runtime.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Il2Cppmscorlib"><HintPath>$(GameDir)BepInEx\interop\Il2Cppmscorlib.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$(GameDir)BepInEx\interop\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="DungeonSettlersDelvers.Core"><HintPath>$(GameDir)BepInEx\plugins\DungeonSettlersDelvers\DungeonSettlersDelvers.Core.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
```

Ändere nur die Zeile `GameDir`. `Private` steht auf `false`, damit dein Paket diese Dateien nicht mitkopiert: Sie gehören dem Spiel, BepInEx und Core. Einige Verweise braucht der Code in dieser Anleitung noch nicht, etwa `Il2Cppmscorlib` und `UnityEngine.CoreModule`; sie stehen schon da, damit du später darauf aufbauen kannst.

## 4. Der Code

Lege im selben Ordner die Datei `Plugin.cs` an und füge ein:

```csharp
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using DungeonSettlersDelvers.Core;

namespace MeinPaket;

[BepInPlugin("mein-name.dungeonsettlers.mein-paket", "Mein Paket", "0.1.0")]
[BepInProcess("DungeonSettlers.exe")]
[BepInDependency(DelversCoreRuntime.BepInExPluginId, ">=0.5.0")]
public sealed class Plugin : BasePlugin
{
    private const string PackId = "mein-paket";
    private static bool schalter;
    private IDisposable seite;

    public override void Load()
    {
        if (!DelversCoreRuntime.IsReady || !DelversCoreRuntime.SupportsApi(DelversCoreRuntime.MinimumApiVersionForMenuPages))
        {
            Log.LogError("Mein Paket bleibt aus: Delvers Core 0.5.0 oder neuer fehlt.");
            return;
        }
        seite = DelversCoreRuntime.RegisterMenuPage(PackId, "Mein Paket", Rows);
        Log.LogInfo("Mein Paket ist bereit.");
    }

    private static IEnumerable<IReadOnlyDictionary<string, object>> Rows()
    {
        yield return DelversMenuRows.Section("Mein Paket");
        yield return DelversMenuRows.Text(() => "Mein erstes Paket läuft.");
        yield return DelversMenuRows.Toggle("Ein Schalter", () => schalter, wert => schalter = wert);
    }

    public override bool Unload()
    {
        seite?.Dispose();
        return base.Unload();
    }
}
```

Was hier passiert:

- `BepInPlugin` gibt deinem Paket eine eindeutige Kennung, einen Namen und eine Version. Ersetze `mein-name` durch deinen Namen.
- `BepInDependency` sorgt dafür, dass BepInEx zuerst Core lädt und dein Paket ohne Core gar nicht startet.
- `Load` prüft, ob Core bereit ist und die Menüseiten kennt (ab Core-API 1.5.0). Dann meldet es die Seite an.
- `seite` hebt die Anmeldung auf. `Unload` gibt sie frei, wenn BepInEx dein Paket entlädt, und nimmt die Seite damit wieder aus dem Menü. Behalte diese Gewohnheit bei: Jede Anmeldung bei Core gibt so einen Rückgabewert, den du aufbewahrst.
- `Rows` beschreibt den Inhalt der Seite: eine Überschrift, einen Text und einen Schalter. Das Menü fragt diese Zeilen jedes Mal ab, wenn die Seite geöffnet wird, und solange sie offen ist, liest es Text und Schalter viermal in der Sekunde neu.

Der Schalter merkt sich seinen Wert nur, solange das Spiel läuft. Wie du Einstellungen dauerhaft speicherst, entscheidest du später selbst, zum Beispiel mit einer eigenen Datei.

## 5. Bauen

Öffne eine Eingabeaufforderung im Ordner `C:\Mods\MeinPaket` (im Explorer in die Adresszeile `cmd` tippen und Enter drücken) und gib ein:

```
dotnet build -c Release
```

Am Ende steht `Der Buildvorgang wurde erfolgreich ausgeführt` oder `Build succeeded`, mit 0 Fehlern und 0 Warnungen. Die fertige Datei liegt unter `bin\Release\net6.0\MeinPaket.dll`.

Wenn ein Fehler erscheint, prüfe zuerst `GameDir`. Die meisten Fehler bedeuten, dass eine der Dateien aus Schritt 3 nicht unter dem angegebenen Pfad liegt.

## 6. Installieren

Beende das Spiel, falls es läuft. Lege im Spielordner den Ordner `BepInEx\plugins\MeinPaket` an und kopiere `MeinPaket.dll` hinein. Kopiere nur diese eine Datei.

## 7. Im Spiel ansehen

Starte das Spiel. Öffne im Titelmenü die Optionen und dort den letzten Reiter „Delvers“. Unter dem Reiter von Core steht jetzt der Reiter „Mein Paket“ mit deinem Text und deinem Schalter. Auf der Seite von Core steht unter „Mods“ jetzt „Mein Paket“.

Während einer laufenden Kampagne ist der Reiter „Delvers“ gesperrt. Kehre dafür ins Titelmenü zurück.

Erscheint der Reiter nicht, sieh in `BepInEx\LogOutput.log` nach:

- Steht dort `Mein Paket ist bereit.`, ist die Seite angemeldet. Öffne das Delvers-Menü noch einmal.
- Steht dort `Mein Paket bleibt aus`, fehlt Core oder es ist älter als 0.5.0.
- Steht dort gar nichts von deinem Paket, liegt die DLL nicht in `BepInEx\plugins` oder BepInEx hat sie nicht geladen. Die Zeilen von BepInEx davor nennen den Grund.

## 8. Wie es weitergeht

- Welche Schnittstellen Core sonst anbietet, zum Beispiel feste Traits, Lade-Callbacks oder umbenannte Schlüssel, steht im [Addon-Leitfaden](CORE-ADDON-LEITFADEN.md).
- Arbeitest du mit einer KI, gib ihr den [KI-Leitfaden](CORE-ADDON-AI-GUIDE.md) mit. Er sagt ihr, was sie verwenden darf und was nicht.
- Fehlt dir eine Stelle des Spiels, an der Core dein Paket einhängen lassen soll, frag sie über die Vorlage „Core-Hook anfragen“ in den Issues an.
- Speicherst du eigene Daten im Spielstand, plane von Anfang an, was passiert, wenn jemand dein Paket wieder entfernt. Sichere vor jedem Test deine Spielstände.

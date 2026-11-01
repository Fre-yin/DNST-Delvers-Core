# Your first Delvers addon in 30 minutes

[Deutsch](CORE-ADDON-SCHNELLSTART.md) | **English**

This guide takes you from an empty folder to your own addon that appears as its own tab in the Delvers menu. You need no prior knowledge of C#. Each step says what to type and what you should see afterwards.

What the addon does at the end: it loads with the game, checks that Delvers Core is there, and shows its own page in the Delvers menu with a text and a switch. You can build on it from there.

## 1. What you need

- Windows 10 or 11, 64 bit.
- Dungeon Settlers version DS_B.0.4.29. With another game version Core stays off and says so in the log.
- BepInEx 6 for Unity IL2CPP x64 (tested with 6.0.0-be.788), installed in the game folder. Start the game once with BepInEx until the main menu appears, then quit. Only then does the folder `BepInEx\interop` exist.
- Delvers Core 0.5.0, installed as described in its README. In the game the options then show "Delvers" as the last tab.
- The .NET SDK 6 or newer from [dotnet.microsoft.com](https://dotnet.microsoft.com/download). Check the installation in a command prompt:

  ```
  dotnet --version
  ```

  A version number appears, for example `8.0.100`. With an SDK newer than 6, the first build downloads files for .NET 6 once; keep a connection ready.
- A text editor. Windows Notepad is enough, Visual Studio Code is more comfortable.

In this guide the game is in `C:\Games\Dungeon Settlers`. Replace this path everywhere with your own game folder, the folder that contains `DungeonSettlers.exe`.

## 2. Create the folder

Create an empty folder for your addon, for example `C:\Mods\MyAddon`. You create only two files in it.

Choose a name for your addon and a pack ID. The pack ID consists of lowercase letters, digits and hyphens, for example `my-addon`. It must not be used by Core or another addon.

## 3. The project file

In the folder, create the file `MyAddon.csproj` and paste:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>MyAddon</AssemblyName>
    <Nullable>disable</Nullable>
    <!-- .NET 6 is old, but the game needs it. This line hides the warning about it. -->
    <CheckEolTargetFramework>false</CheckEolTargetFramework>
    <!-- Your game folder, ending with \ -->
    <GameDir>C:\Games\Dungeon Settlers\</GameDir>
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

Change only the `GameDir` line. `Private` is `false` so that your addon does not copy these files: they belong to the game, BepInEx and Core. The code in this guide does not need some of these references yet, such as `Il2Cppmscorlib` and `UnityEngine.CoreModule`; they are there so you can build on them later.

## 4. The code

In the same folder, create the file `Plugin.cs` and paste:

```csharp
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using DungeonSettlersDelvers.Core;

namespace MyAddon;

[BepInPlugin("my-name.dungeonsettlers.my-addon", "My Addon", "0.1.0")]
[BepInProcess("DungeonSettlers.exe")]
[BepInDependency(DelversCoreRuntime.BepInExPluginId, ">=0.5.0")]
public sealed class Plugin : BasePlugin
{
    private const string PackId = "my-addon";
    private static bool switchOn;
    private IDisposable page;

    public override void Load()
    {
        if (!DelversCoreRuntime.IsReady || !DelversCoreRuntime.SupportsApi(DelversCoreRuntime.MinimumApiVersionForMenuPages))
        {
            Log.LogError("My Addon stays off: Delvers Core 0.5.0 or newer is missing.");
            return;
        }
        page = DelversCoreRuntime.RegisterMenuPage(PackId, "My Addon", Rows);
        Log.LogInfo("My Addon is ready.");
    }

    private static IEnumerable<IReadOnlyDictionary<string, object>> Rows()
    {
        yield return DelversMenuRows.Section("My Addon");
        yield return DelversMenuRows.Text(() => "My first addon is running.");
        yield return DelversMenuRows.Toggle("A switch", () => switchOn, value => switchOn = value);
    }

    public override bool Unload()
    {
        page?.Dispose();
        return base.Unload();
    }
}
```

What happens here:

- `BepInPlugin` gives your addon a unique identity, a name and a version. Replace `my-name` with your own name.
- `BepInDependency` makes BepInEx load Core first, and your addon does not start at all without Core.
- `Load` checks that Core is ready and knows menu pages (from Core API 1.5.0). Then it registers the page.
- `page` keeps the registration. `Unload` releases it when BepInEx unloads your addon, which takes the page out of the menu again. Keep this habit: every registration with Core returns such a value, and you keep it.
- `Rows` describes the page: a heading, a text and a switch. The menu asks for these rows every time the page opens, and while it is open it reads the text and the switch again four times a second.

The switch keeps its value only while the game runs. How you store settings permanently is up to you later, for example in a file of your own.

## 5. Build

Open a command prompt in the folder `C:\Mods\MyAddon` (type `cmd` into the Explorer address bar and press Enter) and enter:

```
dotnet build -c Release
```

At the end it says `Build succeeded`, with 0 errors and 0 warnings. The finished file is `bin\Release\net6.0\MyAddon.dll`.

If an error appears, check `GameDir` first. Most errors mean that one of the files from step 3 is not at the given path.

## 6. Install

Quit the game if it is running. In the game folder, create the folder `BepInEx\plugins\MyAddon` and copy `MyAddon.dll` into it. Copy only this one file.

## 7. See it in the game

Start the game. In the title menu, open the options and there the last tab "Delvers". Below Core's tab there is now the tab "My Addon" with your text and your switch. On Core's page, "My Addon" now appears under "Mods".

During a running campaign the "Delvers" tab is locked. Return to the title menu for it.

If the tab does not appear, look in `BepInEx\LogOutput.log`:

- If it says `My Addon is ready.`, the page is registered. Open the Delvers menu again.
- If it says `My Addon stays off`, Core is missing or older than 0.5.0.
- If it says nothing about your addon, the DLL is not in `BepInEx\plugins` or BepInEx did not load it. The BepInEx lines before it name the reason.

## 8. Where to go next

- Which other interfaces Core offers, for example fixed traits, load callbacks or renamed keys, is described in the [addon guide](CORE-ADDON-LEITFADEN.md) (German).
- If you work with an AI, give it the [AI guide](CORE-ADDON-AI-GUIDE.md). It tells the AI what it may use and what not.
- If you need a place in the game where Core should let your addon hook in, ask for it with the issue template "Request a Core hook".
- If you store your own data in the save, plan from the start what happens when someone removes your addon again. Back up your saves before every test.

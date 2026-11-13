This page explains how to add windows, widgets, and notification items to this app with a plugin. A plugin is a .NET class library (DLL) written in C#, and it runs inside the app.

## What you can make

| Type | Interface the entry class implements | Description |
|---|---|---|
| Window | `IPluginWindowProvider` | A window that opens when you click the plugin's row in "[Plugins](Plugins)". You can build its content freely ([Plugin Development (Window)](Plugin-Development-(Window))). |
| Widget | `IPluginWidgetProvider`, `IPluginMultiWidgetProvider` | A widget whose card appears in "[Widgets](Widgets)". There is a form with a single window and a form that opens a window per target ([Plugin Development (Widget)](Plugin-Development-(Widget))). |
| Notification item | `IPluginNotificationProvider` | An item listed in "[Notifications](Notifications)" ([Plugin Development (Notification)](Plugin-Development-(Notification))). |
| Background only | None | Has no screen and only does processing, such as reading game data and writing it to the log. |

- One plugin can combine a window, widgets, and notifications. The entry class implements all the interfaces you use.
- How to read game data, names, and game messages is described in [Plugin Development (Data)](Plugin-Development-(Data)).

## What you need

- Windows (64-bit) and the .NET 10 SDK.
- The SDK package (`StarResonanceDps.PluginSdk.<version>.nupkg`). It is listed in [Releases](https://github.com/kokoukohiro/StarResonanceDps/releases) alongside the app's zip. It is not on the official NuGet site.
- The app, for testing. Use the SDK from the same release as the app that will use the plugin (see "SDK version" below).

## Creating the project

1. Create a class library project that targets `net10.0-windows` and uses WPF.
2. Create a `packages` folder in the project folder and put the SDK `.nupkg` in it.
3. Put a `nuget.config` in the project folder and add `packages` as a package source.
4. Write a `PackageReference` to the SDK in the csproj. Set `Version` to the version of the `.nupkg` you placed.

`nuget.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local" value="packages" />
  </packageSources>
</configuration>
```

csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="StarResonanceDps.PluginSdk" Version="1.0.0" />
  </ItemGroup>
</Project>
```

- The SDK package adds the setting that copies dependency DLLs to the build output (`EnableDynamicLoading`). You do not need to write it in the csproj.
- The assembly name (the project name by default) becomes the name of the DLL and of its folder in the app, `Plugins\<assembly name>\`.
- The package includes descriptions (in English) of the SDK types and members. They appear when you hover over them in an editor such as Visual Studio.

## Entry class and attributes

```csharp
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(MyPlugin.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "私のプラグイン")]
[assembly: PluginDisplayName("en-US", "My Plugin")]
[assembly: PluginAuthor("YourName")]
[assembly: PluginVersion("1.0.0")]

namespace MyPlugin;

public sealed class Plugin : IStarResonancePlugin
{
    public void Initialize(IPluginContext context)
    {
        context.Logger.Info("Started.");
    }

    public void Shutdown()
    {
    }
}
```

- The entry class must be a public, non-nested class in this DLL that implements `IStarResonancePlugin` and the screen interfaces you use, and it must have a public parameterless constructor. Abstract and generic classes cannot be used.
- The attributes are applied to the assembly.

| Attribute | What to write | Required |
|---|---|---|
| `PluginRegistration` | The entry class and `PluginSdkVersion.Current` | Required |
| `PluginDisplayName` | A language name and the name shown in the list (one per language) | At least one |
| `PluginGenre` | A language name and the genre shown in the list (one per language) | At least one for a plugin with a window. Not used if there is no window |
| `PluginAuthor` | The author's name | Required |
| `PluginVersion` | The version, as three numbers joined by "." (`1.0.0`), without `v` | Required |
| `PluginIcon` | The path of the icon image | Optional. The app's icon is used if omitted |

- The language names are `ja-JP`, `ko-KR`, `zh-CN`, and `en-US`. If there is no name for the app's display language, the app chooses, in this order, the name for the language alone (such as `ja`), `en-US`, and then any of the rest. If you write only one, that name appears in every display language.
- Attributes can only take constants, so names and genres cannot be taken from resx.
- The icon is an image included in the plugin DLL as a WPF resource. Write `<Resource Include="Assets\icon.ico" />` in the csproj, and write the path from the project (`Assets/icon.ico`) in the attribute. Use a `.ico` that has images of several sizes, or a single `.png`. It appears in the row of the list, at the top left of the plugin's window, and as the window's icon.

### Build checks

The SDK package includes checks for the attributes and definitions. An error stops the build. The app checks the same things when it loads the plugin.

| ID | Severity | Description |
|---|---|---|
| SRDPS001 | Error | `PluginRegistration` is missing |
| SRDPS003 | Error | The version in `PluginRegistration` differs from the referenced SDK. Pass `PluginSdkVersion.Current` instead of writing a number directly |
| SRDPS004 | Error | The entry class does not meet the rules above |
| SRDPS005 | Error | `PluginDisplayName` is missing |
| SRDPS006 | Error | There are two `PluginDisplayName` or `PluginGenre` attributes for the same language |
| SRDPS007 | Error | `PluginAuthor` is missing or empty |
| SRDPS008 | Error | A plugin with a window has no `PluginGenre` |
| SRDPS009 | Warning | A plugin without a window has a `PluginGenre` (it is not used) |
| SRDPS010 | Warning | A `PluginDisplayName` or `PluginGenre` has an empty language name or text (it is not used) |
| SRDPS011 | Warning | The path in `PluginIcon` is empty |
| SRDPS012 | Error | `PluginVersion` is missing or is not three numbers |
| SRDPS013 | Error | `GetWidgetTargets` of a widget that opens a window per target always returns an empty list |
| SRDPS014 | Error | Required text, such as the name of a widget, notification, or setting, or the default text of a notification, is empty |

## What the app provides

You receive it through `IPluginContext`, the argument of `Initialize`.

| Member | Contents |
|---|---|
| `Settings` | Settings files placed in the plugin's folder ("Settings" below) |
| `Messages` | Message windows that look the same as the app's ("Messages" below) |
| `Localization` | The app's display language and an event when it changes ("Language" below) |
| `Logger` | The app's log ("Log" below) |
| `GameData` | Game data ([Plugin Development (Data)](Plugin-Development-(Data))) |
| `Names` | Looks up names from game IDs ([Plugin Development (Data)](Plugin-Development-(Data))) |
| `Packets` | Reads game messages ([Plugin Development (Data)](Plugin-Development-(Data))) |
| `Notifications` | Sends the notifications you defined ([Plugin Development (Notification)](Plugin-Development-(Notification))) |
| `Stopping` | Canceled when the app stops this plugin ("How it runs" below) |

### Settings

```csharp
var settings = context.Settings.Load<MySettings>("settings.json");
context.Settings.Save("settings.json", settings);
```

- The files are placed in `Plugins\<assembly name>\` in the app folder (`Settings.SettingsDirectory`). Write a file name that does not include a folder.
- `Load` returns `new T()` if the file does not exist. It throws `InvalidDataException` if the file cannot be read or cannot be parsed as JSON.
- `Save` throws an exception if it cannot write.
- JSON is read and written with `System.Text.Json`. Property names are not case-sensitive when reading.
- The settings files remain after an update, and the whole folder is removed when the plugin is deleted.
- Do not use `uninstall.marker`, `app_terminated.marker`, or names that start with `update.pending`, because the app uses them in the same folder.

### Language

- `Localization.CurrentCulture` is the app's display language: one of `ja-JP`, `ko-KR`, `zh-CN`, and `en-US`. When it changes, `CultureChanged` arrives on the UI thread. It also arrives when a language is chosen in Settings (before saving).
- `PluginLocalizer` reads resx files for the four languages. Embed the resx files in the plugin DLL. A normal .NET build creates a separate DLL for each language, so give each language its own name in the csproj and embed it.

```xml
<ItemGroup>
  <EmbeddedResource Update="Properties\Resources.resx">
    <WithCulture>false</WithCulture>
    <LogicalName>MyPlugin.Properties.Resources.resources</LogicalName>
  </EmbeddedResource>
  <EmbeddedResource Update="Properties\Resources.ja-JP.resx">
    <WithCulture>false</WithCulture>
    <LogicalName>MyPlugin.Properties.Resources.ja-JP.resources</LogicalName>
  </EmbeddedResource>
  <EmbeddedResource Update="Properties\Resources.ko-KR.resx">
    <WithCulture>false</WithCulture>
    <LogicalName>MyPlugin.Properties.Resources.ko-KR.resources</LogicalName>
  </EmbeddedResource>
  <EmbeddedResource Update="Properties\Resources.zh-CN.resx">
    <WithCulture>false</WithCulture>
    <LogicalName>MyPlugin.Properties.Resources.zh-CN.resources</LogicalName>
  </EmbeddedResource>
</ItemGroup>
```

```csharp
var texts = new PluginLocalizer(context.Localization, typeof(Plugin).Assembly, "MyPlugin.Properties.Resources");
string title = texts["WindowTitle"];
```

- `Resources.resx` without a language is `en-US`. Embed all four languages. An exception occurs if the resx for the display language is missing.
- If a key is not in the display language, the `en-US` text is returned, and if it is not there either, the key itself is returned.
- The indexer (`texts["WindowTitle"]`) can also be used in XAML bindings. When the language changes, it is read again through `PropertyChanged`.
- When you are done with it, call `Dispose` to unsubscribe from the language event.

### Log

- `Logger.Info`, `Warning`, and `Error` write to the app's log. Each line starts with `[Plugin:<assembly name>]`.
- The log appears in "[Logs](Logs)" and in `Data\CombatRuntime.log` in the app folder. `Data\CombatRuntime.log` is cleared when the app version changes.

### Messages

- `Messages.Show(title, message, detail)` shows a message window that looks the same as the app's. The detail can be omitted.
- It can be called from any thread. When called from the UI thread, it usually does not return until the window is closed.
- There is no window that asks the user to choose "Yes" or "No".

## How it runs

- At startup, the app initializes all plugins one at a time, in the order they were installed (it creates the entry class, calls `Initialize`, and reads the widget and notification definitions). A plugin added with "Add plugin" is initialized right away.
- The app calls plugin code only on the UI thread (`Initialize`, creating the content of windows and widgets, events, and so on). The app's screen stops until the call returns, so do heavy work on another thread.
- Notifications cannot be sent yet inside `Initialize`. The notification definitions are read after `Initialize`.
- `Shutdown` is called when the app exits and when the app stops the plugin. It is also called when `Initialize` ends with an exception. `Stopping` is canceled before that, so stop the timers and tasks that the plugin started in `Stopping` or `Shutdown`.
- Exceptions
  - An exception in code called by the app, an exception on the UI thread from plugin code, or an exception from a `Task` that was not awaited stops only that plugin. The plugin's row in "Plugins" shows "Error", and the "Plugin Error" window appears. The plugin is loaded again at the next startup.
  - An exception on a thread created by the plugin closes the app. That plugin is not loaded until it is reinstalled or updated to a new version.
  - An exception in `Shutdown` is only written to the log.
- A stopped plugin is not restarted while the app is running. Loaded DLLs are not released until the app is closed.
- Code on the UI thread that never returns (an endless loop or wait) cannot be stopped by the app, and the whole app freezes.

## How to test

1. Build. `<assembly name>.dll` is created in the output folder.
2. In "[Plugins](Plugins)" in the app, choose that DLL with "Add plugin". Dependency DLLs are copied with it from the same folder.
3. To test a rebuilt DLL, close the app, overwrite the DLL in `<app folder>\Plugins\<assembly name>\`, and start the app. The DLL cannot be replaced while the app is running.

- You can see exceptions and logs in "[Logs](Logs)".

## Distribution and updates

- Distribute `<assembly name>.dll` and, if it has dependencies, the `.deps.json` and the dependency DLLs (as they are arranged in the output folder). A plugin without dependencies needs only the one DLL.
- Do not distribute `StarResonanceDps.PluginSdk.dll`. The app's copy is used.
- Only the SDK, .NET, and WPF are taken from the app. Distribute every other dependency (such as a library added from NuGet) with the plugin, even if the app has the same one.
- Users choose `<assembly name>.dll` with "Add plugin". It cannot be added if the dependencies are not in the same folder as the chosen DLL.
- When you distribute a new version, raise `PluginVersion`. When a user chooses the new DLL with "Add plugin", it replaces the old one when the app restarts.
- The settings files remain after an update. Make sure the new version can read the settings files of the previous version.
- If you change the assembly name, the plugin is treated as a different plugin (it is not an update, and the settings are not carried over).
- When a plugin is deleted (with the trash button in "Plugins"), its whole folder, including the settings files, is removed when the app restarts.

## SDK version

- The first number of the SDK package version is `PluginSdkVersion.Current`. When features are added, the second and later numbers go up.
- The app loads a plugin whose `PluginSdkVersion.Current` passed to `PluginRegistration` is the same as the app's, and whose SDK version is the same as or older than the app's SDK. Otherwise, the plugin's row in "Plugins" shows "Error", and the "Plugin Error" window shows "This is not a plugin for this app, or it does not match the app version."
- Build with the SDK from the same release as the users' app so that their app can load the plugin. A plugin built with a newer SDK cannot be loaded by an older app.
- To switch to a new SDK, replace the `.nupkg` in `packages` and change `Version` in the csproj to the new version. If `Version` is left unchanged, the build uses the previous SDK from the NuGet cache without any warning.

## Notes

- Give the assembly a name that does not collide with other plugins. Plugins with the same assembly name are treated as the same plugin (a newer version replaces the older one). A plugin with the same name as one of the app's components is not loaded.
- The DLL file name must be the same as the assembly name.
- If a dependency DLL that has XAML resources might also be used by other plugins in a different version, write `AssemblyVersion` in the dependency's csproj. The version then goes into the WPF resource references, so the dependency is not mixed up with a different version of the same component. When you refer to the dependency's resources yourself, also use the form that includes the version (`/<component name>;v<AssemblyVersion>;component/…`).
- Plugins can use only the SDK types and the appearance keys in `PluginResourceKeys`. The app's internal types and other resource keys can change without notice.
- Plugins run inside the app and can do anything the app can do.

## Samples

The repository's [StarResonanceDps.Plugins.Samples](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples) contains samples that build as they are. The comments are in English.

| Sample | Contents |
|---|---|
| [SampleWindow](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleWindow) | The smallest window plugin. The window size, a `.ico` icon, and a list of the `PluginResourceKeys` keys |
| [SampleWidget](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleWidget) | Four widgets (one with everything, a minimal one, and two that open a window per target) and a `.png` icon |
| [SampleNotification](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleNotification) | Two notification items (one with insertable items and extra settings, and a minimal one) |
| [SampleGameData](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleGameData) | A window that shows all the game data and names as text |
| [SamplePackets](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SamplePackets) | Has no screen; reads game messages and writes only their sizes to the log |
| [SampleLocalization](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleLocalization) | resx files for four languages, a settings file, and a message window |
| [SampleDependency](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleDependency) | Uses the types and XAML resources of another DLL ([SampleDependency.Library](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleDependency.Library)) |
| [SampleAll](https://github.com/kokoukohiro/StarResonanceDps/tree/main/StarResonanceDps.Plugins.Samples/SampleAll) | A window, widgets, and notifications in one plugin |

- The csproj files of the samples use the same `PackageReference` form as in "Creating the project" above. When you copy a sample folder to use it, make the SDK `.nupkg` available with the `nuget.config` form above (inside the repository, `Directory.Build.targets` replaces it with a reference to the SDK project).

## License

- The SDK (`StarResonanceDps.PluginSdk`), the analyzers, and the samples are under the MIT License.
- For combining plugins with the app, see the "Plugins" section of [LICENSING.md](https://github.com/kokoukohiro/StarResonanceDps/blob/main/LICENSING.md) in the repository.

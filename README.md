# LC-API-X

LC API but mod checks are configurable. 

## Why?

I don't want to broadcast my mods publicly.

## Features

- Hide/Show mod list to other players
- Custom mod list (useful for faking mod list for paranoid server owners)
- Enable/Disable modded lobby name

## Configs

```csharp
Config.Bind("LC_API_X", "Hide mod list", true, "Disables sending mod list to other players via LC_API_X");
Config.Bind("LC_API_X", "Fake mod list", new List<string>(), "A list of mods to pretend you have installed. This is useful for faking mod list for paranoid server owners. Default sends original mod list.");
Config.Bind("LC_API_X", "Enable modded lobby name", false, "Should the modded lobby name be enabled? This will add [MODDED] to the start of the lobby name.");
```

## Installation

Drop the DLL into BepInEx/plugins

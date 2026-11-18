# Arcane Row

A mod for The Spell Brigade that adds a sixth tier to the Enchantments screen. Four new enchantments: Volley (faster
projectiles), Reach (more range), Attunement (stronger elements) and Alchemy (better health potions), each with its
own Mastery. You buy them with your normal gold once you have 12 Masteries.

The mod keeps its own save file, so your game save stays untouched. Still in development.

## Building

You need the .NET SDK and the game with BepInExPack IL2CPP, started once so BepInEx has made its `interop` folder.
If you don't use r2modman's Default profile, copy `GamePaths.props.user.example` to `GamePaths.props.user` and set
`BepInExDir` to your BepInEx folder. Then, with the game closed:

```bash
dotnet build src/SpellBrigadeArcane -c Release -p:DeployToGame=true
```

Tests: `dotnet test tests/SpellBrigadeArcane.Tests`.

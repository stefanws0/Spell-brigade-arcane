# Arcane Row

A BepInEx 6 mod for The Spell Brigade that adds a sixth row to the Enchantments screen: Volley, Reach, Attunement and
Alchemy, each with levels and a Mastery, bought with your normal gold. It unlocks at 12 Masteries and keeps its own
save file, so your game save is never touched.

Status: in development (milestone A: foundation and game scan). Design: `docs/superpowers/specs/2026-11-18-arcane-row-design.md`.

Build: copy `GamePaths.props.user.example` to `GamePaths.props.user`, point it at your BepInEx folder, then
`dotnet build src/SpellBrigadeArcane -c Release -p:DeployToGame=true` with the game closed.

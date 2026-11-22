# Arcane Row

A sixth tier on the Enchantments screen, with four new enchantments. You buy them with your normal gold once you have
12 Masteries. Each has five levels and a Mastery, like the game's own.

| Enchantment | Levels | Mastery |
|---|---|---|
| Volley | Projectile Speed, up to +30% | Spells that fire several Projectiles fire 1 more |
| Reach | Spell Range, up to +30% | Long Shot: up to 20% more Damage the farther away the enemy is |
| Attunement | Elemental Damage, up to +25% | Prism: +4% Elemental Damage for each different Element on your Spells |
| Alchemy | Health Potions heal up to +50% and drop up to +25% more often | Potion Belt: a potion picked up at full Health is saved and drunk when you drop below 30% |

Middle-click an enchantment to refund its last level. The game's Refund All refunds this tier too.

## Good to know

- The mod keeps its own save file and never changes your game save. If you remove the mod, use Refund All first to
  get your gold back.
- With a gamepad, refund this tier with Refund All; the single-level refund button only works on the game's own
  enchantments.
- I haven't tested it in co-op yet. Everyone who wants the tier needs the mod. Long Shot's extra Damage only counts
  when you host or play alone.

## Settings

`BepInEx/config/com.stefanws0.spellbrigadearcane.cfg`, under `[General]`:

- `LongShot` (default on): Long Shot changes the game's damage code with one small patch. Turn it off if a game
  update makes it misbehave; the rest of the mod keeps working.

## Source and changelog

The code is at https://github.com/stefanws0/Spell-brigade-arcane. What changed in each version is in the
Changelog tab on this page.

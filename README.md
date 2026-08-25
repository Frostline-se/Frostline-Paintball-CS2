# Frostline Paintball for CS2

CounterStrikeSharp-plugin som skapar färgade, projicerade paintballstänk vid varje `bullet_impact`.

## Beteende

- Alltid aktiv när `Enabled` är `true`.
- Inget `!paintball`-kommando och inga spelarinställningar.
- Slumpmässig färg per kulträff.
- Riktig Source 2 `env_decal`, inte laserstrålar eller explosionseffekter.
- Slumpmässig storlek och orientering efter CS2:s riktiga ytnormal.
- Begränsat antal aktiva dekaler; äldsta stänket tas bort först.
- Dekaler rensas som standard vid ny runda.

## Färger

Black, Brown, Dark Green, Golden Rod, Medium Slate Blue, Olive, Red Orange,
Violet, Baby Blue, Blue, Red, White, Lime Green och Purple.

## Krav

- Metamod:Source
- CounterStrikeSharp API 372 eller senare (.NET 10-version)
- MultiAddonManager
- Workshop-addon byggt från `assets/content`

CS2 skickar inte godtyckliga material från servern som gamla Source/FastDL gjorde.
Materialpaketet måste därför publiceras som ett Workshop-addon och monteras med
MultiAddonManager på både server och klient.

## Bygg plugin

```powershell
.\tools\Build-PluginRelease.ps1
```

Det färdiga serverträdet skapas direkt under:

```text
release/server/addons/counterstrikesharp/plugins/FrostlinePaintball/
```

Kopiera innehållet i `release/server` till serverns `game/csgo`.

## Bygg Source 2-material

Installera CS2 Workshop Tools och kör:

```powershell
.\tools\Compile-Assets.ps1 -Cs2Root "G:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive"
```

Skriptet skapar också ett lokalt CS2-addon i
`content/csgo_addons/frostline_paintball_assets`, vilket kan öppnas och publiceras
med Workshop Tools. `release/workshop-addon` är en portabel kopia av de
kompilerade filerna. Lägg sedan det publicerade Workshop-ID:t i:

```text
game/csgo/cfg/multiaddonmanager/multiaddonmanager.cfg
```

Exempel:

```text
mm_extra_addons "DITT_WORKSHOP_ID"
```

Byt karta efter att addonet monterats så att materialen precachas.

## Konfiguration

CounterStrikeSharp skapar:

```text
game/csgo/addons/counterstrikesharp/configs/plugins/FrostlinePaintball/FrostlinePaintball.json
```

`MaxActiveDecals`, storlek, projektionsdjup, rundrensning och varje enskild färg
kan ändras där. Materialnamnen måste matcha Workshop-addonet.

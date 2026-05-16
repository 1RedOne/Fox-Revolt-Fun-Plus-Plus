# Fox Revolt Fun++
![](assets/hero.png)
## Overview

Fox Revolt Fun++ is an unofficial, Fun First, BepInEx balance mod for Royal Revolt Survivors.

It focuses on making runs feel faster, more generous, and less punishing while keeping the changes configurable through a simple JSON file, also makes it easier to rezz your friends to keep the good times rolling.


# How to install

- Download Bepin Ex Here - post link 
- Unzip the package to the games install location, alongside files like 

```
"Steam\steamapps\common\Royal Revolt Survivors\rrw.exe"
"Steam\steamapps\common\Royal Revolt Survivors\UnityPlayer.dll"
```

The Bepinex files like `winhttp.dll` should be placed in this same directory.

Launch the game once and then quit.  Then check and see if there is a log file in the Bepin ex diretory which woudl be

```
"Steam\steamapps\common\Royal Revolt Survivors\BepInEx\LogOutput.log"
```

If so...you're golden!  Now extract the mods content into the plugins directory, like this

```
"Steam\steamapps\common\Royal Revolt Survivors\BepInEx\plugins\FoxRevoltFunPlusPlus.dll"
```

Installing on Steam deck?  I got you!  [How to install BepinEx mods on SteamDeck](linkToBlogPost.lol) 

## What The Mod Changes

### Faster Combat

The mod improves player attack pacing in two ways:

- base attack speed gets an added `+15%` in addition to whatever store bonuses you purchase
- level-up picks add more speed over the run

### Level-Up Speed And Area Scaling

Every level-up pick contributes a small bonus to both attack speed and attack area.

Default scaling:

- first three effective level-up picks: `+3%` speed and area each
- every later effective level-up pick: `+0.5%` speed and area each

This is intended to make early level-ups feel more impactful without letting the later scaling explode too aggressively.

### Stronger Healing

Default:

```
HealingMultiplier = 3.0
```


### Better Health Regeneration

The mod injects an added health regeneration multiplier into player stats.

Default:

```
HealthRegenerationMultiplier = 2.0
```

A fully buffed Recharge ring will take you from 0 to full health in about 30 seconds now...just don't get hit.

### Co-Op Revive Penalty Removal

The mod patches the co-op rescue world event so rescuing a teammate no longer punishes the rescuer with the original health penalty.  Tested this locally only, I'm not sure if it will work in online play.

The revived player returns with configurable health.

Default:

```text
CoopReviveHealthPercent = 0.5
```

### Moe Cheers Buff

Moe's special move, Cheers/DrinkBrew, is patched because its original effects are weak and include a dangerous health-related penalty.

# Let's get drank

- adds `+1` max/current charge
- halves recharge time
- halves cooldown
- triples positive Cheers stat effects
- doubles Cheers buff duration
- removes negative health-related Cheers penalties

## Configuration

The mod reads its tuning values from:

```text
BepInEx/config/buffs.json
```

If the file does not exist, the mod creates it with default values.

Current default config:

```json
//to do fix these, out of sate
{
    "HealingMultiplier": 3.0,
    "AttackSpeedMultiplier": 1.25,
    "BasePlayerAttackSpeedBonus": 0.15000000596046449,
    "EarlyLevelPickSpeedAreaBonus": 0.029999999329447748,
    "LaterLevelPickSpeedAreaBonus": 0.009999999776482582,
    "HealthRegenerationMultiplier": 2.0,
    "CoopReviveHealthPercent": 0.5,
    "MoeWarriorIdContains": "Hero_Monk",
    "MoeSecretMoveAbilityIdContains": "Cheers,DrinkBrew",
    "MoeSecretMoveExtraCharges": 1,
    "MoeSecretMoveRechargeTimeMultiplier": 0.5,
    "MoeSecretMoveCooldownMultiplier": 0.5,
    "CheersAbilityIdContains": "Cheers,DrinkBrew",
    "CheersBuffStatMultiplier": 3.0,
    "CheersBuffDurationMultiplier": 2.0,
    "CheersRemoveHealthPenalty": true,
    "CheersBuffNegativeStats": false,
    "VerboseLogging": false,
    "HeartbeatLogging": false
}
```

## How It Works

The game is written in Unity, which means that BepInEx will...just work!  But looks like I was the first one to write a mod for this game!

## Build And Deployment

The project builds a BepInEx plugin DLL:

```text
FoxRevoltFunPlusPlus.dll
```

The project deploy target copies the built DLL to:

```text
BepInEx/plugins/FoxRevoltFunPlusPlus.dll
```

But you probably don't care about that!  For you, just click on releases to the side and download the newest bits.


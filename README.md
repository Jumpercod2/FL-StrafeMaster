# FL StrafeMaster — CS2 BHOP Chat HUD (CounterStrikeSharp)

[![License](https://img.shields.io/badge/License-MIT-informational)](#license)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue)]()
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-API%20160+-blueviolet)]()

**FL StrafeMaster** is a lightweight **CS2 plugin** for **CounterStrikeSharp**  
that displays compact statistics in the chat after each jump:

- **Speed** (horizontal landing speed)  
- **Gain** (velocity gain/loss during air time)  
- **Sync** (strafe synchronization in %)  
- **Jumps** (number of logged jumps)  

The plugin is **autobhop-safe** (detects landing + jump within the same tick)  
and perfect for **Surf & BHOP servers**.

---

## ✨ Features
- Per-jump chat HUD: **Speed / Gain / Sync / Jumps**
- **Autobhop-safe** landing detection
- Individual toggle with `!strafehud`
- Subtle tip every **60s**, only for players with HUD enabled
- Clean rotation detection (cross/dot calculation)

---

## 🔧 Commands
- `!strafehud` / `strafehud` / `css_strafehud` – Toggle HUD on/off
- `!r` / `r` / `css_r` – Reset personal HUD stats

---

## 📦 Installation
1. Clone the repo or download a release and build:
   ```bash
   dotnet build -c Release

Copy the generated DLL to your CS2 server:
- cs2/game/csgo/addons/counterstrikesharp/plugins/FLstrafeMaster.dll
Restart or reload your server.

⚙️ Configuration

All settings are defined as const values in the code:

MinAirTicksToReport – minimum air ticks required to count a jump

PrintThrottleTicks – throttling of chat output (counting is unaffected)

JumpImpulseDelta, FallingVzThreshold – thresholds for autobhop detection

🤝 Community

Discord: Join here https://discord.gg/DMvjvQEV2P

Contact me directly: Jumper420

👤 Credits

Author: Jumper

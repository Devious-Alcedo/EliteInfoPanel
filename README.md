![CodeQL](https://github.com/jimmyeao/EliteInfoPanel/actions/workflows/github-code-scanning/codeql/badge.svg)

EliteInfoPanel
Coming soon to a small display on your desk! The goal is a mulit-functional display to help commanders with colonization efforts, and to provide easily accessible visual information whether on foot, in ship, SRV, figter or a wing!
Current progress:

* In Ship - mainly complete, provided route information, cargo information, statuses and fuel info
* Carrier Cargo - Provides a mechanic for tracking cargo - you will need to initially update the carrier cargo manually either by editin gthe Fleet Carrier Cargo Card, or by transferring cargo to a ship, then back to the carrier 
* Colonisation Tracking - mainly complete, but can only currently track one colonisation effort at a time (future enhancement)
* Ability to export Tracking Data to CSV
* Can pop out colinization card and fleet carrier cargo card
* On Foot backpack in development

https://github.com/Devious-Alcedo/EliteInfoPanel/releases/tag/firstalpha

🖱️ Quick Access Keys

Esc - Exit full screen

F2 – Open Settings

F11 – Quit

F12 – Show Logs and Debug Info

Can run as a full screen app on a small display or windowed on a regular display

![image](https://github.com/user-attachments/assets/e587d949-b922-4621-a188-a4fed7e2237d)

🌌 Colonization Card
Currently supports tracking a single colonization build (to be expanded in future updates).

Bar Colors:

🟥 Red, 🟧 Amber, 🟨 Yellow, 🟩 Green – Progress levels of deliveries.

🟦 Blue – Represents how much is "in stock" (on your ship or carrier).

![WhatsApp Image 2025-05-26 at 17 25 48_8e1d9fa3](https://github.com/user-attachments/assets/7da8ef3a-49e8-4cba-87f9-bc0472847e5d)

Export to CSV:

![WhatsApp Image 2025-05-26 at 17 26 58_bf3ef9b4](https://github.com/user-attachments/assets/7e58c472-ab4e-4f06-9055-b245d3b363ec)


🚀 Fleet Carrier Jump Overlays
Enjoy dynamic overlays if you are onboard your fleet carrier when it jumps!


![Screen Recording 2025-04-21 141334 (1)](https://github.com/user-attachments/assets/ec8cc69e-3033-405d-828d-ed1d3407dbe9)

MQTT integration

Can push Status flags to MQTT for use in home automation!

![image](https://github.com/user-attachments/assets/efb40c7f-661f-4384-8e5e-e8a01e43a780)

Persistence and upgrade notes
- App settings and persisted state are stored under AppData/Roaming/EliteInfoPanel:
  - CarrierCargo.json (fleet carrier commodity quantities)
  - ManualCarrierCargo.json (temporary manual overrides, 30 min expiry)
  - RouteProgress.json (route pruning progress)
  - ColonizationData.json (active depot cache)
- Safe migrations are built in:
  - If CarrierCargo.json is missing, legacy carrier_cargo_state.json is imported automatically and then deleted.
  - If ManualCarrierCargo.json is missing, legacy LocalAppData/EliteCompanion/ManualCarrierCargo.json is imported automatically and then deleted.
- All JSON IO uses shared-read semantics to avoid file locks with the game.

Troubleshooting
- Logs
  - App logs: %LocalAppData%/EliteInfoPanel/EliteInfoPanel_Log*.log
  - Open latest from the app with F12.
- Reset state
  - Close the app, then delete files under %AppData%/EliteInfoPanel:
    - CarrierCargo.json, ManualCarrierCargo.json, RouteProgress.json, ColonizationData.json, settings.json (optional).
  - This resets saved state and preferences; game files are untouched.
- Development mode
  - Edit %AppData%/EliteInfoPanel/settings.json: set DevelopmentMode to true.
  - Optionally set DevelopmentJournalPath to a folder with Elite Dangerous JSON files to simulate game data.
- Overlays stuck
  - Hyperspace overlay: ensure you’re not in jump; F12 shows logs to confirm state.
  - Carrier jump overlay: press F7 to force reset; F8 prints debug info to the log.
- MQTT
  - Use the Test button in Settings to verify connectivity and TLS settings.


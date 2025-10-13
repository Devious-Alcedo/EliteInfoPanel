using System;
using System.Linq;
using System.Text.Json;
using EliteInfoPanel.Services;
using EliteInfoPanel.Util;
using Serilog;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService
    {
        private bool LoadStatusData()
        {
            var oldStatus = CurrentStatus;
            CurrentStatus = LoadJsonFile<StatusJson>("Status.json", CurrentStatus);

            bool changed = !ReferenceEquals(oldStatus, CurrentStatus);

            if (changed)
            {
                if (CurrentStatus?.Flags != null)
                {
                    uint rawFlags = (uint)CurrentStatus.Flags;
                    Log.Debug("Raw Status Flags value: 0x{RawFlags:X8} ({RawFlags})",
                        rawFlags, rawFlags);

                    Log.Debug("Active flags: {Flags}",
                        Enum.GetValues(typeof(Flag))
                            .Cast<Flag>()
                            .Where(f => f != Flag.None && CurrentStatus.Flags.HasFlag(f))
                            .Select(f => f.ToString())
                            .ToList());

                    if (CurrentStatus.Flags2 != 0)
                    {
                        Log.Debug("Raw Status Flags2 value: 0x{RawFlags2:X8} ({RawFlags2})",
                            CurrentStatus.Flags2, CurrentStatus.Flags2);
                    }
                }
                else
                {
                    Log.Warning("Status.json loaded but Flags property is null");
                }

                // Notify dependent properties
                OnPropertyChanged(nameof(FuelMain));
                OnPropertyChanged(nameof(CurrentShip));
                OnPropertyChanged(nameof(Balance));
                OnPropertyChanged(nameof(CurrentSystem));
                OnPropertyChanged(nameof(CommanderName));
                OnPropertyChanged(nameof(FuelReserve));
                // Publish to MQTT if enabled
                PublishStatusToMqtt(CurrentStatus);
            }

            return changed;
        }

        private void ProcessLegalStateEvent(JsonElement root, string eventType)
        {
            try
            {
                // Different events have different ways to get the legal status
                switch (eventType)
                {
                    case "Status":
                        // Status.json flag for legal status
                        if (root.TryGetProperty("LegalState", out var legalStateProp))
                        {
                            LegalState = legalStateProp.GetString() ?? "Clean";
                            Log.Debug("Legal state from Status.json: {0}", LegalState);
                        }
                        break;

                    case "Docked":
                        // When docked, reset to "Clean" unless explicitly told otherwise
                        if (root.TryGetProperty("Wanted", out var wantedProp) && wantedProp.GetBoolean())
                        {
                            LegalState = "Wanted";
                        }
                        else
                        {
                            LegalState = "Clean";
                        }
                        if (root.TryGetProperty("StationName", out var stationProp))
                        {
                            CurrentStationName = stationProp.GetString();

                            // Check specifically for Fleet Carrier station type
                            bool isCarrier = false;
                            if (root.TryGetProperty("StationType", out var stationTypeProp))
                            {
                                string stationType = stationTypeProp.GetString();
                                isCarrier = string.Equals(stationType, "FleetCarrier", StringComparison.OrdinalIgnoreCase);

                                Log.Debug("Docked at station: {Station}, StationType: {Type}, IsCarrier: {IsCarrier}",
                                    CurrentStationName, stationType, isCarrier);
                            }

                            // Only set if true or if we're sure it's not a carrier
                            if (isCarrier || stationTypeProp.ValueKind != JsonValueKind.Undefined)
                            {
                                IsOnFleetCarrier = isCarrier;
                            }
                        }
                        break;

                    case "FactionKillBond":
                    case "Bounty":
                        // These are activities against wanted ships
                        LegalState = "Clean"; // Reaffirm we're clean
                        break;

                    case "CommitCrime":
                        // Process different crime types
                        if (root.TryGetProperty("CrimeType", out var crimeTypeProp))
                        {
                            string crimeType = crimeTypeProp.GetString();
                            switch (crimeType?.ToLower())
                            {
                                case "assault":
                                case "murder":
                                case "piracy":
                                    LegalState = "Wanted";
                                    break;

                                case "speeding":
                                    LegalState = "Speeding";
                                    break;

                                case "illegalcargo":
                                    LegalState = "IllegalCargo";
                                    break;

                                default:
                                    LegalState = "Wanted"; // Default for other crimes
                                    break;
                            }
                            Log.Debug("Legal state changed due to crime: {0}", LegalState);
                        }
                        break;

                    case "FactionAllianceChanged":
                        if (root.TryGetProperty("Status", out var statusProp))
                        {
                            string status = statusProp.GetString();
                            if (status?.ToLower() == "hostile")
                            {
                                LegalState = "Hostile";
                            }
                            else if (status?.ToLower() == "allied")
                            {
                                LegalState = "Allied";
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing legal state from event {0}", eventType);
            }
        }

        private async void PublishStatusToMqtt(StatusJson status)
        {
            if (!_mqttInitialized || status == null)
                return;

            try
            {
                // Force publish with the current docking state
                await MqttService.Instance.PublishFlagStatesAsync(status, IsDocking, forcePublish: true);

                // Also publish commander status if we have the data
                if (!string.IsNullOrEmpty(CommanderName) && !string.IsNullOrEmpty(CurrentSystem))
                {
                    string shipInfo = !string.IsNullOrEmpty(ShipLocalised) ? ShipLocalised :
                                     !string.IsNullOrEmpty(ShipName) ? ShipNameHelper.GetLocalisedName(ShipName) : "Unknown";

                    await MqttService.Instance.PublishCommanderStatusAsync(
                         CommanderName,
                         CurrentSystem,
                         shipInfo,
                         Balance ?? 0,
                         FuelMain,
                         FuelReserve);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error publishing status to MQTT");
            }
        }
    }
}

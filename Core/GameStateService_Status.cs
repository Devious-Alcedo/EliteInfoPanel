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
            // Prefer the centralized files service for JSON load
            CurrentStatus = _statusService.LoadStatus(_filesService) ?? CurrentStatus;

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
                var mapped = _statusService.MapLegalState(root, eventType);
                if (!string.IsNullOrWhiteSpace(mapped.LegalState))
                {
                    LegalState = mapped.LegalState;
                    Log.Debug("Legal state set: {0} from {1}", LegalState, eventType);
                }
                if (!string.IsNullOrWhiteSpace(mapped.StationName))
                {
                    CurrentStationName = mapped.StationName;
                }
                if (mapped.IsOnFleetCarrier.HasValue)
                {
                    IsOnFleetCarrier = mapped.IsOnFleetCarrier.Value;
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

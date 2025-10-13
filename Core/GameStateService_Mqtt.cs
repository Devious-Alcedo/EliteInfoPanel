using System;
using System.Threading.Tasks;
using Serilog;
using EliteInfoPanel.Services;
using EliteInfoPanel.Util;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService
    {
        private async Task InitializeMqttAsync()
        {
            try
            {
                var settings = SettingsManager.Load();
                if (settings.MqttEnabled)
                {
                    await MqttService.Instance.InitializeAsync(settings);
                    _mqttInitialized = true;
                    Log.Information("MQTT service initialized for GameStateService");

                    if (CurrentStatus != null)
                    {
                        await MqttService.Instance.PublishFlagStatesAsync(CurrentStatus);
                        Log.Information("Initial state published to MQTT after MQTT initialization.");
                    }
                    else
                    {
                        Log.Warning("Cannot publish initial state: CurrentStatus is null.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize MQTT service in GameStateService");
            }
        }

        public async Task PublishCurrentStateToMqtt()
        {
            if (_mqttInitialized && CurrentStatus != null)
            {
                try
                {
                    await MqttService.Instance.PublishFlagStatesAsync(CurrentStatus, IsDocking);
                    Log.Information("Published current state to MQTT");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error publishing current state to MQTT");
                }
            }
        }

        public async Task RefreshMqttSettingsAsync()
        {
            try
            {
                var settings = SettingsManager.Load();
                await MqttService.Instance.InitializeAsync(settings);
                _mqttInitialized = settings.MqttEnabled;
                Log.Information("MQTT settings refreshed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error refreshing MQTT settings");
            }
        }
    }
}

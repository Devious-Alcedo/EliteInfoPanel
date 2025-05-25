using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;

using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using Serilog;

namespace EliteInfoPanel.Services
{
    public class MqttService : IDisposable
    {
        private static readonly Lazy<MqttService> _instance = new Lazy<MqttService>(() => new MqttService());
        public static MqttService Instance => _instance.Value;

        private IManagedMqttClient _mqttClient;
        private AppSettings _settings;
        private readonly object _lockObject = new object();
        private DateTime _lastPublish = DateTime.MinValue;
        private Dictionary<Flag, bool> _lastFlagStates = new Dictionary<Flag, bool>();
        private Dictionary<Flags2, bool> _lastFlags2States = new Dictionary<Flags2, bool>();
        private bool _isInitialized = false;
        private readonly Timer _reconnectTimer;

        public bool IsConnected => _mqttClient?.IsConnected ?? false;
        public event EventHandler<bool> ConnectionStateChanged;

        private MqttService()
        {
            // Initialize reconnect timer (check every 30 seconds)
            _reconnectTimer = new Timer(CheckConnection, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public async Task InitializeAsync(AppSettings settings)
        {
            if (_isInitialized && _settings?.MqttBrokerHost == settings.MqttBrokerHost &&
                _settings?.MqttBrokerPort == settings.MqttBrokerPort)
            {
                // Just update settings if only non-connection settings changed
                _settings = settings;
                return;
            }

            _settings = settings;

            if (!_settings.MqttEnabled)
            {
                await DisconnectAsync();
                return;
            }

            try
            {
                await SetupMqttClientAsync();
                _isInitialized = true;
                Log.Information("MQTT Service initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize MQTT service");
                throw;
            }
        }

        private async Task SetupMqttClientAsync()
        {
            // Dispose existing client if it exists
            if (_mqttClient != null)
            {
                await _mqttClient.StopAsync();
                _mqttClient.Dispose();
            }

            var factory = new MqttFactory();

            // Create managed client options
            var clientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId(_settings.MqttClientId)
                .WithTcpServer(_settings.MqttBrokerHost, _settings.MqttBrokerPort)
                .WithCleanSession();

            // Add credentials if provided
            if (!string.IsNullOrEmpty(_settings.MqttUsername))
            {
                clientOptionsBuilder.WithCredentials(_settings.MqttUsername, _settings.MqttPassword);
            }

            // Add TLS if enabled
            if (_settings.MqttUseTls)
            {
                clientOptionsBuilder.WithTls();
            }

            var clientOptions = clientOptionsBuilder.Build();

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions)
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(10))
                .Build();

            _mqttClient = factory.CreateManagedMqttClient();

            // Set up event handlers
            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
            _mqttClient.ConnectingFailedAsync += OnConnectingFailedAsync;

            // Start the client
            await _mqttClient.StartAsync(managedOptions);
        }

        private Task OnConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            Log.Information("MQTT client connected to {Host}:{Port}", _settings.MqttBrokerHost, _settings.MqttBrokerPort);
            ConnectionStateChanged?.Invoke(this, true);
            return Task.CompletedTask;
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            Log.Warning("MQTT client disconnected: {Reason}", arg.Reason);
            ConnectionStateChanged?.Invoke(this, false);
            return Task.CompletedTask;
        }

        private Task OnConnectingFailedAsync(ConnectingFailedEventArgs arg)
        {
            Log.Error(arg.Exception, "MQTT connection failed");
            return Task.CompletedTask;
        }

        public async Task PublishFlagStatesAsync(StatusJson status)
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected || status == null)
                return;

            // Rate limiting
            if ((DateTime.UtcNow - _lastPublish).TotalMilliseconds < _settings.MqttPublishIntervalMs)
                return;

            try
            {
                var currentFlags = GetCurrentFlagStates(status);
                var currentFlags2 = GetCurrentFlags2States(status);

                // Check if we should publish (only on changes if configured)
                if (_settings.MqttPublishOnlyChanges && !HasFlagChanges(currentFlags, currentFlags2))
                    return;

                // Publish individual flag states
                await PublishIndividualFlagsAsync(currentFlags, currentFlags2);

                // Publish combined status
                await PublishCombinedStatusAsync(status, currentFlags, currentFlags2);

                // Update last states
                _lastFlagStates = currentFlags;
                _lastFlags2States = currentFlags2;
                _lastPublish = DateTime.UtcNow;

                Log.Debug("Published MQTT flag states");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error publishing MQTT flag states");
            }
        }

        private Dictionary<Flag, bool> GetCurrentFlagStates(StatusJson status)
        {
            var states = new Dictionary<Flag, bool>();

            foreach (Flag flag in Enum.GetValues<Flag>())
            {
                if (flag == Flag.None) continue;

                // Handle synthetic flags
                if (flag == SyntheticFlags.HudInCombatMode)
                {
                    states[flag] = !status.Flags.HasFlag(Flag.HudInAnalysisMode);
                }
                else if (flag == SyntheticFlags.Docking)
                {
                    // You'd need to inject docking state here or get it from GameStateService
                    states[flag] = false; // Placeholder - implement based on your docking logic
                }
                else
                {
                    states[flag] = status.Flags.HasFlag(flag);
                }
            }

            return states;
        }

        private Dictionary<Flags2, bool> GetCurrentFlags2States(StatusJson status)
        {
            var states = new Dictionary<Flags2, bool>();

            foreach (Flags2 flag in Enum.GetValues<Flags2>())
            {
                if (flag == Flags2.None) continue;
                states[flag] = (status.Flags2 & (int)flag) != 0;
            }

            return states;
        }

        private bool HasFlagChanges(Dictionary<Flag, bool> currentFlags, Dictionary<Flags2, bool> currentFlags2)
        {
            // Check if any primary flags changed
            foreach (var kvp in currentFlags)
            {
                if (!_lastFlagStates.TryGetValue(kvp.Key, out bool lastState) || lastState != kvp.Value)
                    return true;
            }

            // Check if any Flags2 changed
            foreach (var kvp in currentFlags2)
            {
                if (!_lastFlags2States.TryGetValue(kvp.Key, out bool lastState) || lastState != kvp.Value)
                    return true;
            }

            return false;
        }

        private async Task PublishIndividualFlagsAsync(Dictionary<Flag, bool> flags, Dictionary<Flags2, bool> flags2)
        {
            var tasks = new List<Task>();

            // Publish primary flags
            foreach (var kvp in flags.Where(f => f.Value)) // Only publish active flags
            {
                var topic = $"{_settings.MqttTopicPrefix}/flags/{kvp.Key.ToString().ToLower()}";
                var payload = JsonSerializer.Serialize(new
                {
                    flag = kvp.Key.ToString(),
                    active = kvp.Value,
                    timestamp = DateTime.UtcNow.ToString("O")
                });

                tasks.Add(PublishAsync(topic, payload));
            }

            // Publish Flags2
            foreach (var kvp in flags2.Where(f => f.Value)) // Only publish active flags
            {
                var topic = $"{_settings.MqttTopicPrefix}/flags2/{kvp.Key.ToString().ToLower()}";
                var payload = JsonSerializer.Serialize(new
                {
                    flag = kvp.Key.ToString(),
                    active = kvp.Value,
                    timestamp = DateTime.UtcNow.ToString("O")
                });

                tasks.Add(PublishAsync(topic, payload));
            }

            await Task.WhenAll(tasks);
        }

        private async Task PublishCombinedStatusAsync(StatusJson status, Dictionary<Flag, bool> flags, Dictionary<Flags2, bool> flags2)
        {
            var topic = $"{_settings.MqttTopicPrefix}/status";

            var payload = JsonSerializer.Serialize(new
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                flags = flags.Where(f => f.Value).Select(f => f.Key.ToString()).ToArray(),
                flags2 = flags2.Where(f => f.Value).Select(f => f.Key.ToString()).ToArray(),
                raw_flags = (uint)status.Flags,
                raw_flags2 = status.Flags2,
                fuel = status.Fuel?.FuelMain ?? 0,
                balance = status.Balance,
                on_foot = status.OnFoot
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            await PublishAsync(topic, payload);
        }

        private async Task PublishAsync(string topic, string payload)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected)
                return;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)_settings.MqttQosLevel)
                .WithRetainFlag(_settings.MqttRetainMessages)
                .Build();

            await _mqttClient.EnqueueAsync(message);
        }

        public async Task PublishCommanderStatusAsync(string commanderName, string system, string ship)
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected)
                return;

            try
            {
                var topic = $"{_settings.MqttTopicPrefix}/commander";
                var payload = JsonSerializer.Serialize(new
                {
                    commander = commanderName,
                    system = system,
                    ship = ship,
                    timestamp = DateTime.UtcNow.ToString("O")
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                await PublishAsync(topic, payload);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error publishing commander status to MQTT");
            }
        }

        public async Task DisconnectAsync()
        {
            if (_mqttClient != null)
            {
                try
                {
                    await _mqttClient.StopAsync();
                    Log.Information("MQTT client disconnected");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error disconnecting MQTT client");
                }
            }
        }

        private void CheckConnection(object state)
        {
            if (_settings?.MqttEnabled == true && (_mqttClient == null || !_mqttClient.IsConnected))
            {
                Log.Debug("MQTT connection check - attempting reconnection");
                Task.Run(async () =>
                {
                    try
                    {
                        await SetupMqttClientAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "MQTT reconnection attempt failed");
                    }
                });
            }
        }

        public void Dispose()
        {
            _reconnectTimer?.Dispose();

            if (_mqttClient != null)
            {
                try
                {
                    _mqttClient.StopAsync().Wait(5000);
                    _mqttClient.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error disposing MQTT client");
                }
            }
        }
    }
}
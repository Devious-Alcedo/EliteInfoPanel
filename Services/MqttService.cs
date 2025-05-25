using EliteInfoPanel.Core;
using EliteInfoPanel.Util;
using MQTTnet;

using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EliteInfoPanel.Services
{
    public class MqttService : IDisposable
    {
        private static readonly Lazy<MqttService> _instance = new Lazy<MqttService>(() => new MqttService());
        public static MqttService Instance => _instance.Value;
        private readonly HashSet<string> _haConfigSent = new(); // Track sent configs
        private IMqttClient _mqttClient;
        private AppSettings _settings;
        private readonly object _lockObject = new object();
        private DateTime _lastPublish = DateTime.MinValue;
        private Dictionary<Flag, bool> _lastFlagStates = new Dictionary<Flag, bool>();
        private Dictionary<Flags2, bool> _lastFlags2States = new Dictionary<Flags2, bool>();
        private bool _isInitialized = false;
        private readonly Timer _reconnectTimer;
        private CancellationTokenSource _cancellationTokenSource;
        private string deviceName = "eliteinfopanel";
        public bool IsConnected => _mqttClient?.IsConnected ?? false;
        public event EventHandler<bool> ConnectionStateChanged;

        private MqttService()
        {
            _cancellationTokenSource = new CancellationTokenSource();
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
        private async Task PublishHomeAssistantConfigIfNeeded(Enum flag, string stateTopic)
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected)
                return;

           
            string baseTopic = _settings.MqttTopicPrefix?.TrimEnd('/') ?? "homeassistant";
            string sensor = flag.ToString().ToLowerInvariant();
            string configTopic = $"{baseTopic}/binary_sensor/{deviceName}/{sensor}/config";

            if (_haConfigSent.Contains(configTopic)) return;

            var configPayload = new
            {
                name = sensor,
                state_topic = stateTopic,
                unique_id = $"{deviceName}_{sensor}",
                device_class = "connectivity",
                payload_on = "ON",
                payload_off = "OFF",
                device = new
                {
                    identifiers = new[] { deviceName },
                    name = deviceName,
                    manufacturer = "Elite Dangerous",
                    model = "Elite Info Panel"
                }
            };

            string configJson = JsonSerializer.Serialize(configPayload);
            await PublishAsync(configTopic, configJson, retain: true);
            _haConfigSent.Add(configTopic);
        }

        private async Task SetupMqttClientAsync()
        {
            // Dispose existing client if it exists
            if (_mqttClient != null)
            {
                if (_mqttClient.IsConnected)
                {
                    await _mqttClient.DisconnectAsync(MqttClientDisconnectOptionsReason.NormalDisconnection);
                }
                _mqttClient.Dispose();
            }

            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            // Create client options using MQTTnet 5 API
            var clientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId(_settings.MqttClientId)
                .WithTcpServer(_settings.MqttBrokerHost, _settings.MqttBrokerPort)
                .WithCleanSession(true)
                .WithProtocolVersion(MqttProtocolVersion.V500); // Use MQTT 5.0

            // Add credentials if provided
            if (!string.IsNullOrEmpty(_settings.MqttUsername))
            {
                clientOptionsBuilder.WithCredentials(_settings.MqttUsername, _settings.MqttPassword);
            }

            // Add TLS if enabled
            if (_settings.MqttUseTls)
            {
                clientOptionsBuilder.WithTlsOptions(o =>
                {
                        o.UseTls(); // Correctly invoke the method instead of assigning
                        o.WithAllowUntrustedCertificates(false);
                        o.WithIgnoreCertificateChainErrors(false);
                    // With the correct method call:
                    o.WithIgnoreCertificateRevocationErrors(false);
                                      
                });
                  
            }

            var clientOptions = clientOptionsBuilder.Build();

            // Set up event handlers
            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

            // Connect the client
            await _mqttClient.ConnectAsync(clientOptions, _cancellationTokenSource.Token);
        }

        private Task OnConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            Log.Information("MQTT client connected to {Host}:{Port}", _settings.MqttBrokerHost, _settings.MqttBrokerPort);
            ConnectionStateChanged?.Invoke(this, true);
            return Task.CompletedTask;
        }
        public async Task PublishInitialState(StatusJson status)
        {
            if (status != null)
            {
                await PublishFlagStatesAsync(status);
                Log.Information("Published initial flag states to MQTT.");
            }
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            Log.Warning("MQTT client disconnected: {Reason}", arg.Reason);
            ConnectionStateChanged?.Invoke(this, false);

            // Auto-reconnect if not a normal disconnection
            if (arg.Reason != MqttClientDisconnectReason.NormalDisconnection &&
                _settings?.MqttEnabled == true && !_cancellationTokenSource.IsCancellationRequested)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), _cancellationTokenSource.Token);
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            await SetupMqttClientAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to reconnect MQTT client");
                        }
                    }
                });
            }

            return Task.CompletedTask;
        }

        public async Task PublishFlagStatesAsync(StatusJson status, bool? isDocking = null)
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected || status == null)
                return;

            // Rate limiting
            if ((DateTime.UtcNow - _lastPublish).TotalMilliseconds < _settings.MqttPublishIntervalMs)
                return;

            try
            {
                var currentFlags = GetCurrentFlagStates(status, isDocking);
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

        private Dictionary<Flag, bool> GetCurrentFlagStates(StatusJson status, bool? isDocking = null)
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
                    states[flag] = isDocking ?? false;
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

            foreach (var kvp in flags)
            {
                string sensor = kvp.Key.ToString().ToLowerInvariant();
                string stateTopic = $"{_settings.MqttTopicPrefix}/binary_sensor/{deviceName}/{sensor}/state";
                string payload = kvp.Value ? "ON" : "OFF";

                await PublishHomeAssistantConfigIfNeeded(kvp.Key, stateTopic);
                tasks.Add(PublishAsync(stateTopic, payload, retain: true));
            }

            foreach (var kvp in flags2)
            {
                string sensor = kvp.Key.ToString().ToLowerInvariant();
                string stateTopic = $"{_settings.MqttTopicPrefix}/binary_sensor/{deviceName}/{sensor}/state";
                string payload = kvp.Value ? "ON" : "OFF";

                await PublishHomeAssistantConfigIfNeeded(kvp.Key, stateTopic); // Now uses correct overload
                tasks.Add(PublishAsync(stateTopic, payload, retain: true));
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

        private async Task PublishAsync(string topic, string payload, bool retain = false)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected)
                return;

            try
            {
                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)_settings.MqttQosLevel)
                    .WithRetainFlag(retain) // Use the retain parameter here
                    .Build();

                await _mqttClient.PublishAsync(applicationMessage, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error publishing MQTT message to topic: {Topic}", topic);
            }
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

        public async Task PublishGameEventAsync(string eventType, object eventData)
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected)
                return;

            try
            {
                var topic = $"{_settings.MqttTopicPrefix}/events/{eventType.ToLower()}";
                var payload = JsonSerializer.Serialize(new
                {
                    eventType = eventType,
                    timestamp = DateTime.UtcNow.ToString("O"),
                    data = eventData
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                await PublishAsync(topic, payload);
                Log.Debug("Published MQTT game event: {EventType}", eventType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error publishing game event to MQTT: {EventType}", eventType);
            }
        }

        public async Task<bool> TestConnectionAsync(AppSettings testSettings)
        {
            var factory = new MqttClientFactory();
            using var testClient = factory.CreateMqttClient();

            try
            {
                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithClientId(testSettings.MqttClientId + "_test")
                    .WithTcpServer(testSettings.MqttBrokerHost, testSettings.MqttBrokerPort)
                    .WithCleanSession(true)
                    .WithProtocolVersion(MqttProtocolVersion.V500);

                if (!string.IsNullOrEmpty(testSettings.MqttUsername))
                {
                    optionsBuilder.WithCredentials(testSettings.MqttUsername, testSettings.MqttPassword);
                }

                if (testSettings.MqttUseTls)
                {
                    optionsBuilder.WithTlsOptions(o =>
                    {
                        o.UseTls(); // Correctly invoke the method instead of assigning
                        o.WithAllowUntrustedCertificates(false);
                        o.WithIgnoreCertificateChainErrors(false);
                        // With the correct method call:
                        o.WithIgnoreCertificateRevocationErrors(false);
                    });
                }

                var options = optionsBuilder.Build();

                // Try to connect with a timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var result = await testClient.ConnectAsync(options, cts.Token);

                if (result.ResultCode == MqttClientConnectResultCode.Success)
                {
                    await testClient.DisconnectAsync(MqttClientDisconnectOptionsReason.NormalDisconnection);
                    return true;
                }
                else
                {
                    Log.Warning("MQTT test connection failed: {ResultCode}", result.ResultCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MQTT test connection error");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                try
                {
                    await _mqttClient.DisconnectAsync(MqttClientDisconnectOptionsReason.NormalDisconnection);
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
                _ = Task.Run(async () =>
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
            _cancellationTokenSource?.Cancel();
            _reconnectTimer?.Dispose();

            if (_mqttClient != null)
            {
                try
                {
                    if (_mqttClient.IsConnected)
                    {
                        _mqttClient.DisconnectAsync(MqttClientDisconnectOptionsReason.NormalDisconnection).Wait(5000);
                    }
                    _mqttClient.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error disposing MQTT client");
                }
            }

            _cancellationTokenSource?.Dispose();
        }
    }
}
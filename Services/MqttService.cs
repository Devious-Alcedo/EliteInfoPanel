using EliteInfoPanel.Core;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Util;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
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
                .WithProtocolVersion(MqttProtocolVersion.V500);

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
                    o.UseTls();
                    o.WithAllowUntrustedCertificates(false);
                    o.WithIgnoreCertificateChainErrors(false);
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

        public async Task PublishFlagStatesAsync(StatusJson status, bool? isDocking = null, bool forcePublish = false)
        {
            if (_settings == null)
                return;
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected || status == null)
                return;

            // Rate limiting (skip if not forcing and within rate limit)
            if (!forcePublish && (DateTime.UtcNow - _lastPublish).TotalMilliseconds < _settings.MqttPublishIntervalMs)
                return;

            try
            {
                var currentFlags = GetCurrentFlagStates(status, isDocking);
                var currentFlags2 = GetCurrentFlags2States(status);

                // Check if we should publish (only on changes if configured, unless forced)
                bool hasChanges = HasFlagChanges(currentFlags, currentFlags2);
                if (!forcePublish && _settings.MqttPublishOnlyChanges && !hasChanges)
                    return;

                if (forcePublish || hasChanges)
                {
                    Log.Information("Publishing MQTT flags - Force={Force}, HasChanges={HasChanges}, ActiveFlags={ActiveFlags}",
                        forcePublish, hasChanges, currentFlags.Count(f => f.Value));
                }

                // Publish individual flag states for Home Assistant
                await PublishIndividualFlagsAsync(currentFlags, currentFlags2);

                // Publish combined status with metadata
                await PublishCombinedStatusAsync(status, currentFlags, currentFlags2);

                // Update last states
                _lastFlagStates = currentFlags;
                _lastFlags2States = currentFlags2;
                _lastPublish = DateTime.UtcNow;

                Log.Debug("Published MQTT flag states: {ActiveFlags} active flags, {ActiveFlags2} active flags2",
                    currentFlags.Count(f => f.Value), currentFlags2.Count(f => f.Value));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error publishing MQTT flag states");
            }
        }

        private Dictionary<Flag, bool> GetCurrentFlagStates(StatusJson status, bool? isDocking = null)
        {
            var states = new Dictionary<Flag, bool>();

            // Get ALL flags from the enum
            foreach (Flag flag in Enum.GetValues<Flag>())
            {
                if (flag == Flag.None) continue;

                // Handle synthetic flags specially
                if (flag == SyntheticFlags.HudInCombatMode)
                {
                    states[flag] = !status.Flags.HasFlag(Flag.HudInAnalysisMode);
                }
                else if (flag == SyntheticFlags.Docking)
                {
                    // Docking should be true only when actively docking, false when docked or not docking
                    bool dockingState = isDocking ?? false;
                    states[flag] = dockingState;
                    Log.Debug("Synthetic Docking flag: isDocking={IsDocking}, result={Result}", isDocking, dockingState);
                }
                else
                {
                    states[flag] = status.Flags.HasFlag(flag);
                }
            }

            // Add debug logging for key states
            Log.Debug("Flag states: Docked={Docked}, Docking={Docking}, IsDockingParam={IsDockingParam}",
                states.GetValueOrDefault(Flag.Docked),
                states.GetValueOrDefault(SyntheticFlags.Docking),
                isDocking);

            return states;
        }

        private Dictionary<Flags2, bool> GetCurrentFlags2States(StatusJson status)
        {
            var states = new Dictionary<Flags2, bool>();

            // Get ALL flags2 from the enum
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
                {
                    Log.Debug("Flag {Flag} changed: {OldState} -> {NewState}", kvp.Key, lastState, kvp.Value);
                    return true;
                }
            }

            // Check if any Flags2 changed
            foreach (var kvp in currentFlags2)
            {
                if (!_lastFlags2States.TryGetValue(kvp.Key, out bool lastState) || lastState != kvp.Value)
                {
                    Log.Debug("Flags2 {Flag} changed: {OldState} -> {NewState}", kvp.Key, lastState, kvp.Value);
                    return true;
                }
            }

            return false;
        }

        private async Task PublishHomeAssistantConfigIfNeeded(string flagName, string stateTopic, string flagType = "flag")
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected)
                return;

            string sensor = flagName.ToLowerInvariant();
            // Change the config topic to not include the device name prefix
            string configTopic = $"homeassistant/binary_sensor/eliteinfopanel/{sensor}/config";

            if (_haConfigSent.Contains(configTopic)) return;

            // Get metadata for this flag
            string icon = "mdi:help";
            string friendlyName = flagName;

            if (flagType == "flag" && Enum.TryParse<Flag>(flagName, true, out var flag))
            {
                if (FlagVisualHelper.TryGetMetadata(flag, out var metadata))
                {
                    icon = $"mdi:{metadata.Icon.ToLowerInvariant()}";
                    friendlyName = metadata.Tooltip ?? flagName;
                }
            }
            else if (flagType == "flags2" && Enum.TryParse<Flags2>(flagName, true, out var flags2))
            {
                if (Flags2VisualHelper.TryGetMetadata(flags2, out var metadata))
                {
                    icon = $"mdi:{metadata.Icon.ToLowerInvariant()}";
                    friendlyName = metadata.Tooltip ?? flagName;
                }
            }

            var configPayload = new
            {
                name = friendlyName, // Use just the friendly name without device prefix
                state_topic = stateTopic,
                unique_id = $"eliteinfopanel_{sensor}", // Keep unique ID with prefix for uniqueness
                object_id = sensor, // Add this to control the entity ID
                icon = icon,
                payload_on = "ON",
                payload_off = "OFF",
                device_class = GetDeviceClass(flagName),
                device = new
                {
                    identifiers = new[] { "eliteinfopanel" }, // Consistent device identifier
                    name = "Elite Info Panel", // More readable device name
                    manufacturer = "Frontier Developments",
                    model = "Elite Dangerous Game State",
                    sw_version = "1.0"
                }
            };

            string configJson = JsonSerializer.Serialize(configPayload);
            await PublishAsync(configTopic, configJson, retain: true);
            _haConfigSent.Add(configTopic);

            Log.Debug("Published Home Assistant config for {FlagType} {Flag}: {FriendlyName}", flagType, flagName, friendlyName);
        }
       

        public async Task ClearAllHomeAssistantEntities()
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected)
                return;

            try
            {
                // Publish empty payloads to remove all existing configs
                foreach (var configTopic in _haConfigSent.ToList())
                {
                    await PublishAsync(configTopic, "", retain: true);
                    Log.Debug("Cleared Home Assistant config: {Topic}", configTopic);
                }

                // Clear the sent configs cache
                _haConfigSent.Clear();

                // Wait a moment for Home Assistant to process
                await Task.Delay(2000);

                // Force republish all current states
                await ForceReconfigureHomeAssistant();

                Log.Information("Cleared and reconfigured all Home Assistant entities");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error clearing Home Assistant entities");
            }
        }
        private string GetDeviceClass(string flagName)
        {
            // Be more conservative with device classes to avoid "Connected" states
            return flagName.ToLower() switch
            {
                "overheating" => "problem",
                "lowfuel" => "problem",
                "isindanger" => "problem",
                "beinginterdicted" => "problem",
                "constructionfailed" => "problem",
                "constructioncomplete" => null, // Let it default to on/off
                "supercruise" => "motion",
                "fsdcharging" => "motion",
                "fsdjump" => "motion",
                "glidemode" => "motion",
                _ => null // Default to no device class for most flags to ensure On/Off display
            };
        }

        private async Task PublishIndividualFlagsAsync(Dictionary<Flag, bool> flags, Dictionary<Flags2, bool> flags2)
        {
            var tasks = new List<Task>();

            // Publish ALL primary flags as Home Assistant binary sensors
            foreach (var kvp in flags)
            {
                string sensor = kvp.Key.ToString().ToLowerInvariant();
                // Use consistent topic structure for all sensors
                string stateTopic = $"{_settings.MqttTopicPrefix}/binary_sensor/{deviceName}/{sensor}/state";
                string payload = kvp.Value ? "ON" : "OFF";

                // Send Home Assistant config first
                await PublishHomeAssistantConfigIfNeeded(kvp.Key.ToString(), stateTopic, "flag");

                // Then send the state
                tasks.Add(PublishAsync(stateTopic, payload, retain: _settings.MqttRetainMessages));
            }

            // Publish ALL Flags2 as Home Assistant binary sensors using the same topic structure
            foreach (var kvp in flags2)
            {
                string sensor = kvp.Key.ToString().ToLowerInvariant();
                // Use same topic structure as primary flags to keep under one device
                string stateTopic = $"{_settings.MqttTopicPrefix}/binary_sensor/{deviceName}/{sensor}/state";
                string payload = kvp.Value ? "ON" : "OFF";

                // Send Home Assistant config first
                await PublishHomeAssistantConfigIfNeeded(kvp.Key.ToString(), stateTopic, "flags2");

                // Then send the state
                tasks.Add(PublishAsync(stateTopic, payload, retain: _settings.MqttRetainMessages));
            }

            await Task.WhenAll(tasks);
        }

        private async Task PublishCombinedStatusAsync(StatusJson status, Dictionary<Flag, bool> flags, Dictionary<Flags2, bool> flags2)
        {
            var topic = $"{_settings.MqttTopicPrefix}/status";

            // Enhanced combined status with metadata
            var payload = JsonSerializer.Serialize(new
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                flags = flags.ToDictionary(
                    f => f.Key.ToString().ToLower(),
                    f => new {
                        active = f.Value,
                        metadata = FlagVisualHelper.TryGetMetadata(f.Key, out var meta) ? new
                        {
                            icon = meta.Icon,
                            tooltip = meta.Tooltip,
                            color = meta.Color.ToString()
                        } : null
                    }
                ),
                flags2 = flags2.ToDictionary(
                    f => f.Key.ToString().ToLower(),
                    f => new {
                        active = f.Value,
                        metadata = Flags2VisualHelper.TryGetMetadata(f.Key, out var meta) ? new
                        {
                            icon = meta.Icon,
                            tooltip = meta.Tooltip,
                            color = meta.Color.ToString()
                        } : null
                    }
                ),
                raw_flags = (uint)status.Flags,
                raw_flags2 = status.Flags2,
                fuel = status.Fuel?.FuelMain ?? 0,
                balance = status.Balance,
                on_foot = status.OnFoot,
                active_flags_count = flags.Count(f => f.Value),
                active_flags2_count = flags2.Count(f => f.Value),
                total_flags_count = flags.Count,
                total_flags2_count = flags2.Count
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            await PublishAsync(topic, payload, retain: _settings.MqttRetainMessages);
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
                    .WithRetainFlag(retain)
                    .Build();

                await _mqttClient.PublishAsync(applicationMessage, _cancellationTokenSource.Token);
                Log.Debug("Published MQTT message to {Topic}: {Payload}", topic, payload);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error publishing MQTT message to topic: {Topic}", topic);
            }
        }

        public async Task PublishCommanderStatusAsync(string commanderName, string system, string ship, decimal credits, double fuel, double fuelreserve)
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected)
                return;

            try
            {
                await PublishHomeAssistantCommanderConfigsIfNeeded();

                var topic = $"{_settings.MqttTopicPrefix}/commander";
                var payload = JsonSerializer.Serialize(new
                {
                    commander = commanderName,
                    system,
                    ship,
                    credits,
                    fuel,
                    fuelreserve,
                    timestamp = DateTime.UtcNow.ToString("O")
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                await PublishAsync(topic, payload, retain: _settings.MqttRetainMessages);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error publishing commander status to MQTT");
            }
        }
        public async Task PublishAllColonizationDepotsAsync(List<ColonizationData> activeDepots)
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected)
                return;

            // Aggregate data for all active depots
            var depotsPayload = new
            {
                depots = activeDepots.Select(depot => new
                {
                    depot.MarketID,
                    depot.ConstructionProgress,
                    depot.ConstructionComplete,
                    depot.ConstructionFailed,
                    ResourcesRequired = depot.ResourcesRequired.Select(r => new
                    {
                        r.Name,
                        r.Name_Localised,
                        r.RequiredAmount,
                        r.ProvidedAmount,
                        r.ShipAmount,
                        r.CarrierAmount,
                        AvailableAmount = r.ShipAmount + r.CarrierAmount,
                        StillNeeded = r.RequiredAmount - r.ProvidedAmount,
                        ReadyToDeliver = (r.ShipAmount + r.CarrierAmount) >= (r.RequiredAmount - r.ProvidedAmount),
                        ValueOfRemainingWork = (r.RequiredAmount - r.ProvidedAmount) * r.Payment,
                        PaymentPerUnit = r.Payment
                    }).ToList()
                }).ToList()
            };

            string topic = $"{_settings.MqttTopicPrefix}/colonisation/all";
            string stateJson = JsonSerializer.Serialize(depotsPayload, new JsonSerializerOptions { WriteIndented = false });

            await PublishAsync(topic, stateJson, retain: _settings.MqttRetainMessages);
            Log.Information("MQTT: Published aggregated colonization data with {DepotCount} depots", activeDepots.Count);
        }
        public async Task PublishColonizationDepotAsync(long marketId, double progress, bool complete, bool failed, List<ColonizationResource> resources)
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected)
                return;

            // Dynamic topic based on MarketID
            string topic = $"{_settings.MqttTopicPrefix}/colonisation/{marketId}";
            string configTopic = $"homeassistant/sensor/eliteinfopanel_colonisation_{marketId}/config";

            // Discovery payload
            var configPayload = new
            {
                name = $"Colonisation Depot {marketId}",
                state_topic = topic,
                unique_id = $"eliteinfopanel_colonisation_{marketId}",
                object_id = $"colonisation_{marketId}",
                value_template = "{{ value_json.ConstructionProgress }}",
                json_attributes_topic = topic,
                device = new
                {
                    identifiers = new[] { "eliteinfopanel" },
                    name = "Elite Info Panel",
                    manufacturer = "Frontier Developments",
                    model = "Elite Dangerous Game State",
                    sw_version = "1.0"
                }
            };

            // Serialize and publish discovery config
            string configJson = JsonSerializer.Serialize(configPayload);
            await PublishAsync(configTopic, configJson, retain: true);
            Log.Information("MQTT: Published Home Assistant discovery for depot {MarketID}", marketId);

            // Prepare state payload
            var statePayload = new
            {
                MarketID = marketId,
                ConstructionProgress = progress,
                ConstructionComplete = complete,
                ConstructionFailed = failed,
                ResourcesRequired = resources.Select(r => new
                {
                    r.Name,
                    r.Name_Localised,
                    r.RequiredAmount,
                    r.ProvidedAmount,
                    r.ShipAmount,
                    r.CarrierAmount,
                    AvailableAmount = r.ShipAmount + r.CarrierAmount,
                    StillNeeded = r.RequiredAmount - r.ProvidedAmount,
                    ReadyToDeliver = (r.ShipAmount + r.CarrierAmount) >= (r.RequiredAmount - r.ProvidedAmount),
                    ValueOfRemainingWork = (r.RequiredAmount - r.ProvidedAmount) * r.Payment,
                    PaymentPerUnit = r.Payment
                }).ToList()
            };

            // Serialize and publish state
            string stateJson = JsonSerializer.Serialize(statePayload);
            await PublishAsync(topic, stateJson, retain: _settings.MqttRetainMessages);
            Log.Information("MQTT: Published colonisation depot {MarketID} state with {ResourceCount} resources", marketId, resources.Count);
        }

        private async Task PublishHomeAssistantCommanderConfigsIfNeeded()
        {
            string baseTopic = $"{_settings.MqttTopicPrefix}/commander";

            var configs = new List<(string id, string name, string valueTemplate)>
    {
        ("commander", "Commander", "{{ value_json.commander }}"),
        ("system", "System", "{{ value_json.system }}"),
        ("ship", "Ship", "{{ value_json.ship }}"),
        ("credits", "Credits", "{{ value_json.credits }}"),
        ("fuel", "Fuel", "{{ value_json.fuel }}"),
        ("fuelreserve", "Fuel Reserve", "{{ value_json.fuelreserve }}")
    };

            foreach (var (id, name, valueTemplate) in configs)
            {
                string configTopic = $"homeassistant/sensor/eliteinfopanel_{id}/config";
                if (_haConfigSent.Contains(configTopic)) continue;

                var configPayload = new
                {
                    name,
                    state_topic = baseTopic,
                    unique_id = $"eliteinfopanel_{id}",
                    object_id = id,
                    value_template = valueTemplate,
                    json_attributes_topic = baseTopic,  // Optional, if you want other fields as attributes
                    device = new
                    {
                        identifiers = new[] { "eliteinfopanel" },
                        name = "Elite Info Panel",
                        manufacturer = "Frontier Developments",
                        model = "Elite Dangerous Game State",
                        sw_version = "1.0"
                    }
                };

                string configJson = JsonSerializer.Serialize(configPayload);
                await PublishAsync(configTopic, configJson, retain: true);
                _haConfigSent.Add(configTopic);

                Log.Debug("Published Home Assistant config for {Name} sensor", name);
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
                        o.UseTls();
                        o.WithAllowUntrustedCertificates(false);
                        o.WithIgnoreCertificateChainErrors(false);
                        o.WithIgnoreCertificateRevocationErrors(false);
                    });
                }

                var options = optionsBuilder.Build();

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

        public async Task ClearHomeAssistantConfigs()
        {
            _haConfigSent.Clear();
            Log.Information("Cleared Home Assistant configuration cache - configs will be resent on next publish");
        }

        public async Task ForceReconfigureHomeAssistant()
        {
            if (!_settings.MqttEnabled || _mqttClient == null || !_mqttClient.IsConnected)
                return;

            try
            {
                // Clear the cache so configs get resent
                _haConfigSent.Clear();

                // If we have current states, republish them to trigger config resend
                if (_lastFlagStates.Any() || _lastFlags2States.Any())
                {
                    var tasks = new List<Task>();

                    // Republish all flag configs and states using consistent topic structure
                    foreach (var kvp in _lastFlagStates)
                    {
                        string sensor = kvp.Key.ToString().ToLowerInvariant();
                        string stateTopic = $"{_settings.MqttTopicPrefix}/binary_sensor/{deviceName}/{sensor}/state";

                        await PublishHomeAssistantConfigIfNeeded(kvp.Key.ToString(), stateTopic, "flag");
                        tasks.Add(PublishAsync(stateTopic, kvp.Value ? "ON" : "OFF", retain: _settings.MqttRetainMessages));
                    }

                    // Use same topic structure for flags2
                    foreach (var kvp in _lastFlags2States)
                    {
                        string sensor = kvp.Key.ToString().ToLowerInvariant();
                        string stateTopic = $"{_settings.MqttTopicPrefix}/binary_sensor/{deviceName}/{sensor}/state";

                        await PublishHomeAssistantConfigIfNeeded(kvp.Key.ToString(), stateTopic, "flags2");
                        tasks.Add(PublishAsync(stateTopic, kvp.Value ? "ON" : "OFF", retain: _settings.MqttRetainMessages));
                    }

                    await Task.WhenAll(tasks);
                    Log.Information("Force reconfigured Home Assistant with {FlagCount} flags and {Flags2Count} flags2",
                        _lastFlagStates.Count, _lastFlags2States.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error force reconfiguring Home Assistant");
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
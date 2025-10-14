using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EliteInfoPanel.Core.Models;
using EliteInfoPanel.Services;
using EliteInfoPanel.Util;
using EliteInfoPanel.Core.EliteInfoPanel.Core;
using Serilog;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService
    {
        private async Task ProcessJournalEventAsync(string eventType, JsonElement root, bool initialScan)
        {
            switch (eventType)
            {
                case "Commander":
                    if (root.TryGetProperty("Name", out var nameProperty))
                    {
                        CommanderName = nameProperty.GetString();
                    }
                    break;
                case "Rank":
                    if (root.TryGetProperty("Combat", out var combatProp))
                        CombatRank = combatProp.GetInt32();
                    if (root.TryGetProperty("Trade", out var tradeProp))
                        TradeRank = tradeProp.GetInt32();
                    if (root.TryGetProperty("Explore", out var exploreProp))
                        ExplorationRank = exploreProp.GetInt32();
                    if (root.TryGetProperty("CQC", out var cqcProp))
                        CqcRank = cqcProp.GetInt32();
                    if (root.TryGetProperty("Exobiologist", out var exobioProp))
                        ExobiologistRank = exobioProp.GetInt32();
                    if (root.TryGetProperty("Mercenary", out var mercProp))
                        MercenaryRank = mercProp.GetInt32();
                    break;
                case "Promotion":
                    if (root.TryGetProperty("Combat", out var combatPromotionProp))
                        CombatRank = combatPromotionProp.GetInt32();
                    if (root.TryGetProperty("Trade", out var tradePromotionProp))
                        TradeRank = tradePromotionProp.GetInt32();
                    if (root.TryGetProperty("Explore", out var explorePromotionProp))
                        ExplorationRank = explorePromotionProp.GetInt32();
                    if (root.TryGetProperty("CQC", out var cqcPromotionProp))
                        CqcRank = cqcPromotionProp.GetInt32();
                    if (root.TryGetProperty("Exobiologist", out var exobioPromotionProp))
                        ExobiologistRank = exobioPromotionProp.GetInt32();
                    if (root.TryGetProperty("Mercenary", out var mercPromotionProp))
                        MercenaryRank = mercPromotionProp.GetInt32();
                    break;
                case "SetUserShipName":
                    if (root.TryGetProperty("Ship", out var setShipTypeProperty) &&
                        root.TryGetProperty("ShipID", out var setShipIdProperty))
                    {
                        string shipType = setShipTypeProperty.GetString();
                        int shipId = setShipIdProperty.GetInt32();
                        string userShipName = root.TryGetProperty("UserShipName", out var nameProp) ? nameProp.GetString() : null;
                        string userShipId = root.TryGetProperty("UserShipId", out var idProp) ? idProp.GetString() : null;
                        ShipName = shipType;
                        UserShipName = userShipName;
                        UserShipId = userShipId;
                    }
                    break;
                case "LoadGame":
                    if (root.TryGetProperty("Ship", out var shipProperty))
                    {
                        ShipName = shipProperty.GetString();
                    }
                    if (root.TryGetProperty("Ship_Localised", out var shipLocalisedProperty))
                    {
                        ShipLocalised = shipLocalisedProperty.GetString();
                    }
                    if (root.TryGetProperty("ShipName", out var shipNameProperty))
                    {
                        UserShipName = shipNameProperty.GetString();
                    }
                    if (root.TryGetProperty("ShipIdent", out var shipIdentProperty))
                    {
                        UserShipId = shipIdentProperty.GetString();
                    }
                    break;
                case "ShipyardSwap":
                    if (root.TryGetProperty("ShipType", out var shipTypeProperty))
                    {
                        string shipType = shipTypeProperty.GetString();
                        string shipTypeName = root.TryGetProperty("ShipType_Localised", out var localisedProp) && !string.IsNullOrWhiteSpace(localisedProp.GetString())
                            ? localisedProp.GetString()
                            : ShipNameHelper.GetLocalisedName(shipType);
                        ShipName = shipType;
                        ShipLocalised = shipTypeName;
                        CurrentLoadout = null;
                        LoadLoadoutData();
                    }
                    break;
                case "Loadout":
                    var loadout = JsonSerializer.Deserialize<LoadoutJson>(root.GetRawText());
                    if (loadout != null)
                    {
                        foreach (var module in loadout.Modules)
                        {
                            if (module.Class == 0 || string.IsNullOrEmpty(module.Rating))
                            {
                                InferClassAndRatingFromItem(module);
                            }
                        }
                        if (!string.IsNullOrEmpty(loadout.ShipName))
                        {
                            UserShipName = loadout.ShipName;
                        }
                        if (!string.IsNullOrEmpty(loadout.ShipIdent))
                        {
                            UserShipId = loadout.ShipIdent;
                        }
                        CurrentLoadout = loadout;
                        OnPropertyChanged(nameof(CurrentLoadout));
                        OnPropertyChanged(nameof(CurrentStatus));
                        LoadoutUpdated?.Invoke();
                    }
                    break;
                case "Undocked":
                    _currentDockingState = DockingState.NotDocking;
                    IsDocking = false;
                    CurrentStationName = null;
                    IsOnFleetCarrier = false;
                    break;
                case "Docked":
                    ProcessDockingEvent(eventType, root);
                    if (root.TryGetProperty("Wanted", out var wantedProp) && wantedProp.GetBoolean())
                        LegalState = "Wanted";
                    else
                        LegalState = "Clean";
                    if (root.TryGetProperty("StationName", out var stationProp))
                    {
                        CurrentStationName = stationProp.GetString();
                        bool isCarrier = false;
                        if (root.TryGetProperty("StationType", out var dockStationTypeProp))
                        {
                            string stationType = dockStationTypeProp.GetString();
                            isCarrier = string.Equals(stationType, "FleetCarrier", StringComparison.OrdinalIgnoreCase);
                        }
                        IsOnFleetCarrier = isCarrier;
                    }
                    break;
                case "DockingCancelled":
                case "DockingDenied":
                case "DockingTimeout":
                case "DockingGranted":
                    ProcessDockingEvent(eventType, root);
                    break;
                case "StartJump":
                    if (root.TryGetProperty("JumpType", out var jumpTypeProp))
                    {
                        string jumpType = jumpTypeProp.GetString();
                        if (jumpType == "Hyperspace")
                        {
                            IsHyperspaceJumping = true;
                            _isInHyperspace = true;
                            if (root.TryGetProperty("StarClass", out var starClassProp))
                            {
                                HyperspaceStarClass = starClassProp.GetString();
                            }
                            else
                            {
                                HyperspaceStarClass = null;
                            }
                            EnsureHyperspaceTimeout();
                        }
                        else
                        {
                            IsHyperspaceJumping = false;
                            _isInHyperspace = false;
                            HyperspaceDestination = null;
                            HyperspaceStarClass = null;
                        }
                    }
                    break;
                case "FSDTarget":
                    if (root.TryGetProperty("RemainingJumpsInRoute", out var jumpsProp))
                        RemainingJumps = jumpsProp.GetInt32();
                    if (root.TryGetProperty("Name", out var fsdNameProp))
                        LastFsdTargetSystem = fsdNameProp.GetString();
                    break;
                case "FSDJump":
                    bool wasBatchMode = _isUpdating;
                    if (wasBatchMode) _isUpdating = false;
                    IsHyperspaceJumping = false;
                    _isInHyperspace = false;
                    HyperspaceDestination = null;
                    HyperspaceStarClass = null;
                    if (wasBatchMode) _isUpdating = true;
                    if (root.TryGetProperty("StarSystem", out JsonElement systemElement))
                    {
                        string currentSystem = systemElement.GetString();
                        if (!string.Equals(LastVisitedSystem, currentSystem, StringComparison.OrdinalIgnoreCase))
                            LastVisitedSystem = currentSystem;
                        CurrentSystem = currentSystem;
                        if (!_routeProgress.CompletedSystems.Contains(CurrentSystem, StringComparer.OrdinalIgnoreCase))
                        {
                            _routeProgress.CompletedSystems.Add(CurrentSystem);
                            _routeProgress.LastKnownSystem = CurrentSystem;
                            SaveRouteProgressDebounced();
                        }
                        PruneCompletedRouteSystems();
                    }
                    break;
                case "SupercruiseEntry":
                    HyperspaceDestination = null;
                    IsHyperspaceJumping = false;
                    HyperspaceStarClass = null;
                    break;
                case "Location":
                    if (IsHyperspaceJumping || _isInHyperspace)
                    {
                        HyperspaceDestination = null;
                        HyperspaceStarClass = null;
                    }
                    if (root.TryGetProperty("StarSystem", out JsonElement locationElement))
                    {
                        string currentSystem = locationElement.GetString();
                        if (!string.Equals(LastVisitedSystem, currentSystem, StringComparison.OrdinalIgnoreCase))
                            LastVisitedSystem = currentSystem;
                        CurrentSystem = currentSystem;
                        if (!string.IsNullOrEmpty(CurrentSystem))
                        {
                            _routeProgress.LastKnownSystem = CurrentSystem;
                            SaveRouteProgressDebounced();
                        }
                        PruneCompletedRouteSystems();
                    }
                    break;
                case "SupercruiseExit":
                    if (IsHyperspaceJumping || _isInHyperspace)
                    {
                        HyperspaceDestination = null;
                        HyperspaceStarClass = null;
                    }
                    if (root.TryGetProperty("StarSystem", out JsonElement exitSystemElement))
                    {
                        CurrentSystem = exitSystemElement.GetString();
                        if (!string.IsNullOrEmpty(CurrentSystem))
                        {
                            _routeProgress.LastKnownSystem = CurrentSystem;
                            SaveRouteProgressDebounced();
                        }
                        PruneCompletedRouteSystems();
                    }
                    break;
                case "CargoDepot":
                case "CarrierTradeOrder":
                    if (initialScan)
                        break;
                    EnsureCarrierCargoTrackingInitialized($"{eventType} event");
                    _carrierCargoService.ApplyEvent(root);
                    using (BeginUpdate())
                    {
                        _carrierCargo = _carrierCargoService.GetState();
                        UpdateCurrentCarrierCargoFromDictionary();
                        SaveCarrierCargoToDisk();
                    }
                    break;
                case "MarketBuy":
                    if (initialScan) break;
                    if (root.TryGetProperty("BuyFromFleetCarrier", out var boughtFromCarrierProp) && boughtFromCarrierProp.GetBoolean())
                    {
                        EnsureCarrierCargoTrackingInitialized("MarketBuy FROM carrier event");
                        _carrierCargoService.ApplyEvent(root);
                        using (BeginUpdate())
                        {
                            _carrierCargo = _carrierCargoService.GetState();
                            UpdateCurrentCarrierCargoFromDictionary();
                            SaveCarrierCargoToDisk();
                        }
                    }
                    break;
                case "MarketSell":
                    if (initialScan) break;
                    if (root.TryGetProperty("SellToFleetCarrier", out var soldToCarrierProp) && soldToCarrierProp.GetBoolean())
                    {
                        EnsureCarrierCargoTrackingInitialized("MarketSell TO carrier event");
                        _carrierCargoService.ApplyEvent(root);
                        using (BeginUpdate())
                        {
                            _carrierCargo = _carrierCargoService.GetState();
                            UpdateCurrentCarrierCargoFromDictionary();
                            SaveCarrierCargoToDisk();
                        }
                    }
                    break;
                case "CargoTransfer":
                    if (initialScan) break;
                    EnsureCarrierCargoTrackingInitialized("CargoTransfer event");
                    _carrierCargoService.ApplyEvent(root);
                    using (BeginUpdate())
                    {
                        _carrierCargo = _carrierCargoService.GetState();
                        UpdateCurrentCarrierCargoFromDictionary();
                        SaveCarrierCargoToDisk();
                    }
                    break;
                case "CarrierJumpRequest":
                    if (root.TryGetProperty("DepartureTime", out var departureTimeProp) &&
                        DateTime.TryParse(departureTimeProp.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var departureTime))
                    {
                        string systemName = root.TryGetProperty("SystemName", out var sysName) ? sysName.GetString() : null;
                        string bodyName = root.TryGetProperty("Body", out var bodyProp) ? bodyProp.GetString() : null;
                        HandleCarrierJumpRequest(departureTime, systemName, bodyName);
                    }
                    break;
                case "CarrierJump":
                    HandleCarrierJumpCompleted();
                    break;
                case "CarrierJumpCancelled":
                case "CarrierCancelJump":
                    HandleCarrierJumpCancelled();
                    break;
                case "CarrierLocation":
                    bool isOnCarrier = false;
                    if (root.TryGetProperty("OnFoot", out var onFootProp) && !onFootProp.GetBoolean() &&
                        root.TryGetProperty("Docked", out var dockedProp) && dockedProp.GetBoolean() &&
                        root.TryGetProperty("StationType", out var stationTypeProp))
                    {
                        string stationType = stationTypeProp.GetString();
                        isOnCarrier = string.Equals(stationType, "FleetCarrier", StringComparison.OrdinalIgnoreCase);
                        IsOnFleetCarrier = isOnCarrier;
                    }
                    if (IsOnFleetCarrier && root.TryGetProperty("StarSystem", out var carrierSystemProp))
                    {
                        var carrierSystem = carrierSystemProp.GetString();
                        CurrentSystem = carrierSystem;
                    }
                    break;
                case "CommitCrime":
                    ProcessLegalStateEvent(root, "CommitCrime");
                    break;
                case "FactionKillBond":
                case "Bounty":
                    ProcessLegalStateEvent(root, eventType);
                    break;
                case "FactionAllianceChanged":
                    ProcessLegalStateEvent(root, "FactionAllianceChanged");
                    break;
                case "Status":
                    ProcessLegalStateEvent(root, "Status");
                    break;
                case "ColonisationConstructionDepot":
                    try
                    {
                        var colonizationData = new ColonizationData { LastUpdated = DateTime.UtcNow };
                        if (root.TryGetProperty("MarketID", out var marketIdProp))
                            colonizationData.MarketID = marketIdProp.GetInt64();
                        if (root.TryGetProperty("ConstructionProgress", out var progressProp))
                            colonizationData.ConstructionProgress = progressProp.GetDouble();
                        if (root.TryGetProperty("ConstructionComplete", out var completeProp))
                            colonizationData.ConstructionComplete = completeProp.GetBoolean();
                        if (root.TryGetProperty("ConstructionFailed", out var failedProp))
                            colonizationData.ConstructionFailed = failedProp.GetBoolean();
                        if (colonizationData.ConstructionComplete || colonizationData.ConstructionFailed)
                        {
                            if (_colonizationDepots.ContainsKey(colonizationData.MarketID))
                                RemoveColonizationDepot(colonizationData.MarketID);
                            try { await MqttService.Instance.PublishColonizationDepotDeletedAsync(colonizationData.MarketID); } catch { }
                            break;
                        }
                        colonizationData.ResourcesRequired = new List<ColonizationResource>();
                        if (root.TryGetProperty("ResourcesRequired", out var resourcesProp) && resourcesProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var resource in resourcesProp.EnumerateArray())
                            {
                                var resourceItem = new ColonizationResource();
                                if (resource.TryGetProperty("Name", out var n)) resourceItem.Name = n.GetString();
                                if (resource.TryGetProperty("Name_Localised", out var nl)) resourceItem.Name_Localised = nl.GetString();
                                if (resource.TryGetProperty("RequiredAmount", out var ra)) resourceItem.RequiredAmount = ra.GetInt32();
                                if (resource.TryGetProperty("ProvidedAmount", out var pa)) resourceItem.ProvidedAmount = pa.GetInt32();
                                if (resource.TryGetProperty("Payment", out var pay)) resourceItem.Payment = pay.GetInt32();
                                colonizationData.ResourcesRequired.Add(resourceItem);
                            }
                        }
                        bool wasBatchMode2 = _isUpdating; if (wasBatchMode2) _isUpdating = false;
                        _colonizationDepots[colonizationData.MarketID] = colonizationData;
                        if (!_selectedDepotMarketId.HasValue || _selectedDepotMarketId.Value == colonizationData.MarketID)
                        {
                            _selectedDepotMarketId = colonizationData.MarketID;
                            OnPropertyChanged(nameof(SelectedColonizationDepot));
                        }
                        OnPropertyChanged(nameof(ColonizationDepots));
                        if (wasBatchMode2) _isUpdating = true;
                        SaveAllColonizationData();
                        await MqttService.Instance.PublishColonizationDepotAsync(
                            colonizationData.MarketID,
                            colonizationData.ConstructionProgress,
                            colonizationData.ConstructionComplete,
                            colonizationData.ConstructionFailed,
                            colonizationData.ResourcesRequired);
                        await MqttService.Instance.PublishAllColonizationDepotsAsync(GetActiveColonizationDepots());
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing ColonisationConstructionDepot event");
                    }
                    break;
                case "ReceiveText":
                case "SquadronStartup":
                case "Music":
                    break;
            }
        }
        public void DebugJournalPosition()
        {
            try
            {
                if (string.IsNullOrEmpty(latestJournalPath) || !File.Exists(latestJournalPath))
                {
                    Log.Error("?? DEBUG: No valid journal file");
                    return;
                }

                var fileInfo = new FileInfo(latestJournalPath);
                Log.Information("?? DEBUG Journal State:");
                Log.Information("  File: {File}", Path.GetFileName(latestJournalPath));
                Log.Information("  File Size: {Size} bytes", fileInfo.Length);
                Log.Information("  Current Position: {Position} bytes", lastJournalPosition);
                Log.Information("  Last Modified: {LastWrite}", fileInfo.LastWriteTime);
                Log.Information("  Bytes Remaining: {Remaining}", fileInfo.Length - lastJournalPosition);

                using var fs = new FileStream(latestJournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long startPos = Math.Max(0, fileInfo.Length - 10240);
                fs.Seek(startPos, SeekOrigin.Begin);

                using var sr = new StreamReader(fs);
                var recentLines = new List<string>();
                var carrierEvents = new List<string>();

                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        recentLines.Add(line);
                        if (line.Contains("CarrierJump") || line.Contains("Carrier"))
                        {
                            carrierEvents.Add(line);
                        }
                    }
                }

                Log.Information("?? DEBUG: Last {LineCount} lines in journal, {CarrierCount} carrier-related events",
                    recentLines.Count, carrierEvents.Count);

                foreach (var carrierEvent in carrierEvents.TakeLast(3))
                {
                    Log.Information("?? CARRIER EVENT: {Event}", carrierEvent);
                }

                bool missedCarrierJump = carrierEvents.Any(e => e.Contains("\"event\":\"CarrierJump\""));
                if (missedCarrierJump)
                {
                    Log.Warning("?? ??  FOUND UNPROCESSED CarrierJump EVENT - journal position may be incorrect!");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "?? DEBUG: Error checking journal position");
            }
        }
        public async Task ProcessJournalAsync()
        {
            if (string.IsNullOrEmpty(latestJournalPath))
                return;

            try
            {
                bool isInitialScan = !_firstLoadCompleted; // true if this is the first pass
                Log.Information("ProcessJournalAsync: initialScan={Initial}, pos={Pos}", isInitialScan, lastJournalPosition);

                using (BeginUpdate())
                {
                    await foreach (var (line, root, pos) in _journalReader.ReadEventsAsync(latestJournalPath, lastJournalPosition, _appStartTimeUtc, isInitialScan))
                    {
                        lastJournalPosition = pos;

                        if (!root.TryGetProperty("event", out var eventProp))
                            continue;

                        string eventType = eventProp.GetString();

                        Log.Debug("Processing journal event: {Event} (InitialScan: {InitialScan})", eventType, isInitialScan);

                        try
                        {
                            await ProcessJournalEventAsync(eventType, root, isInitialScan);

                            if (!_firstLoadCompleted)
                            {
                                _firstLoadCompleted = true;

                                var fileInfo = new FileInfo(latestJournalPath);
                                lastJournalPosition = fileInfo.Length;

                                Log.Information("? First journal scan completed - now monitoring from end (position {Position})",
                                    lastJournalPosition);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Error processing journal file");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading journal file");
            }
        }

        private void ScanJournalForPendingCarrierJump()
        {
            try
            {
                var journalFiles = Directory.GetFiles(gamePath, "Journal.*.log")
                    .OrderBy(f => File.GetLastWriteTime(f));

                DateTime? latestRequestTimestamp = null;
                DateTime? latestDepartureTime = null;
                string latestSystem = null;
                string latestBody = null;
                bool jumpCancelledOrCompleted = false;

                foreach (var path in journalFiles)
                {
                    using var sr = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        if (!root.TryGetProperty("event", out var eventProp)) continue;
                        string eventType = eventProp.GetString();
                        if (eventType == "CarrierJumpRequest")
                        {
                            if (root.TryGetProperty("timestamp", out var tsProp) &&
                                DateTime.TryParse(tsProp.GetString(), out var ts))
                            {
                                latestRequestTimestamp = ts;
                                if (root.TryGetProperty("DepartureTime", out var dtProp) &&
                                    DateTime.TryParse(dtProp.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                                {
                                    if (dt.Kind == DateTimeKind.Local)
                                    {
                                        dt = dt.ToUniversalTime();
                                    }
                                    else if (dt.Kind == DateTimeKind.Unspecified)
                                    {
                                        dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                                    }
                                    latestDepartureTime = dt;
                                }
                                if (root.TryGetProperty("SystemName", out var sysName))
                                    latestSystem = sysName.GetString();
                                if (root.TryGetProperty("Body", out var bodyName))
                                    latestBody = bodyName.GetString();
                                jumpCancelledOrCompleted = false;
                            }
                        }
                        else if ((eventType == "CarrierJumpCancelled" || eventType == "CarrierJump") && latestRequestTimestamp.HasValue)
                        {
                            if (root.TryGetProperty("timestamp", out var tsProp) &&
                                DateTime.TryParse(tsProp.GetString(), out var ts))
                            {
                                if (latestRequestTimestamp.HasValue && ts > latestRequestTimestamp)
                                {
                                    jumpCancelledOrCompleted = true;
                                }
                            }
                        }
                    }
                }
                if (latestRequestTimestamp.HasValue && !jumpCancelledOrCompleted && latestDepartureTime.HasValue && latestDepartureTime > DateTime.UtcNow)
                {
                    _carrierJumpState.ScheduleJump(latestDepartureTime.Value, latestSystem, latestBody);
                    OnPropertyChanged(nameof(JumpCountdown));
                    OnPropertyChanged(nameof(CarrierJumpCountdownSeconds));
                    OnPropertyChanged(nameof(ShowCarrierJumpCountdown));
                    OnPropertyChanged(nameof(ShowCarrierJumpOverlay));
                    Log.Information("Recovered scheduled CarrierJump to {System}, {Body} at {Time}", latestSystem, latestBody, latestDepartureTime);
                }
                else if (latestRequestTimestamp.HasValue && !jumpCancelledOrCompleted && latestDepartureTime.HasValue && latestDepartureTime <= DateTime.UtcNow)
                {
                    Log.Information("Found CarrierJumpRequest but departure time {Time} is in the past - jump already completed", latestDepartureTime);
                    _carrierJumpState.Reset();
                }
                else if (jumpCancelledOrCompleted)
                {
                    Log.Information("Found CarrierJumpCancelled or CarrierJump after last request — not setting jump state");
                    _carrierJumpState.Reset();
                }
                else if (latestRequestTimestamp.HasValue)
                {
                    Log.Information("CarrierJumpRequest found but jump is in the past or cancelled — not setting jump state");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to scan journal for CarrierJumpRequest on startup");
            }
        }
    }
}

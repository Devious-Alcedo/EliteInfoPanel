using System;
using System.Text.Json;
using EliteInfoPanel.Core.Models;

namespace EliteInfoPanel.Core.Services
{
    internal sealed class StatusService : IStatusService
    {
        public StatusJson? LoadStatus(IGameFilesService files)
        {
            return files.ReadJson<StatusJson>("Status.json");
        }

        public LegalStateResult MapLegalState(JsonElement root, string eventType)
        {
            var result = new LegalStateResult();

            switch (eventType)
            {
                case "Status":
                    if (root.TryGetProperty("LegalState", out var legalStateProp))
                    {
                        result.LegalState = legalStateProp.GetString() ?? "Clean";
                    }
                    break;

                case "Docked":
                    if (root.TryGetProperty("Wanted", out var wantedProp) && wantedProp.GetBoolean())
                        result.LegalState = "Wanted";
                    else
                        result.LegalState = "Clean";

                    if (root.TryGetProperty("StationName", out var stationProp))
                        result.StationName = stationProp.GetString();

                    if (root.TryGetProperty("StationType", out var stationTypeProp))
                    {
                        string? stationType = stationTypeProp.GetString();
                        if (!string.IsNullOrWhiteSpace(stationType))
                        {
                            result.IsOnFleetCarrier = string.Equals(stationType, "FleetCarrier", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    break;

                case "FactionKillBond":
                case "Bounty":
                    result.LegalState = "Clean";
                    break;

                case "CommitCrime":
                    if (root.TryGetProperty("CrimeType", out var crimeTypeProp))
                    {
                        string? crimeType = crimeTypeProp.GetString();
                        switch (crimeType?.ToLowerInvariant())
                        {
                            case "assault":
                            case "murder":
                            case "piracy":
                                result.LegalState = "Wanted";
                                break;
                            case "speeding":
                                result.LegalState = "Speeding";
                                break;
                            case "illegalcargo":
                                result.LegalState = "IllegalCargo";
                                break;
                            default:
                                result.LegalState = "Wanted";
                                break;
                        }
                    }
                    break;

                case "FactionAllianceChanged":
                    if (root.TryGetProperty("Status", out var statusProp))
                    {
                        string? status = statusProp.GetString();
                        if (string.Equals(status, "hostile", StringComparison.OrdinalIgnoreCase))
                            result.LegalState = "Hostile";
                        else if (string.Equals(status, "allied", StringComparison.OrdinalIgnoreCase))
                            result.LegalState = "Allied";
                    }
                    break;
            }

            return result;
        }
    }

    internal sealed class LegalStateResult
    {
        public string? LegalState { get; set; }
        public string? StationName { get; set; }
        public bool? IsOnFleetCarrier { get; set; }
    }
}

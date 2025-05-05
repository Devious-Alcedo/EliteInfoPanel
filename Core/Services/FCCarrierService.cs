using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace EliteInfoPanel.Core.Services
{
    public class FCCarrierService
    {
        private readonly HttpClient _httpClient;
        private readonly OAuthTokenManager _tokenManager;
        private readonly string _apiBaseUrl = "https://companion.orerve.net";

        public FCCarrierService(OAuthTokenManager tokenManager)
        {
            _httpClient = new HttpClient();
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
        }

        public async Task<FCCargoJson> GetFleetCarrierCargoAsync()
        {
            try
            {
                // Ensure we have a valid token
                string accessToken = await _tokenManager.GetAccessTokenAsync();

                // Set up the request
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBaseUrl}/fleetcarrier");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Send the request
                var response = await _httpClient.SendAsync(request);

                // Check for errors
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Error("Failed to retrieve Fleet Carrier cargo data: {StatusCode} - {ErrorContent}",
                        response.StatusCode, errorContent);

                    return null;
                }

                // Parse the response
                var responseContent = await response.Content.ReadAsStringAsync();
                var cargoData = JsonSerializer.Deserialize<FCCargoJson>(responseContent);

                Log.Information("Successfully retrieved Fleet Carrier cargo data: {ItemCount} items",
                    cargoData?.Inventory?.Count ?? 0);

                return cargoData;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving Fleet Carrier cargo data");
                return null;
            }
        }
    }
}
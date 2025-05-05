using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Serilog;

namespace EliteInfoPanel.Core.Services
{
    public class OAuthTokenManager
    {
        private const string AUTH_SERVER = "https://auth.frontierstore.net";
        private const string AUTH_ENDPOINT = "/auth";
        private const string TOKEN_ENDPOINT = "/token";

        private readonly string _clientId;
        private readonly string _redirectUri;
        private readonly HttpClient _httpClient;
        private readonly string _tokenStoragePath;

        private string _accessToken;
        private string _refreshToken;
        private DateTime _accessTokenExpiry;

        public OAuthTokenManager(string clientId)
        {
            _clientId = clientId;
            _redirectUri = "https://localhost:8443/auth/callback";
            _httpClient = new HttpClient();

            // Create a storage path in AppData for tokens
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "EliteInfoPanel");
            Directory.CreateDirectory(appFolder);
            _tokenStoragePath = Path.Combine(appFolder, "oauth_tokens.json");

            // Try to load existing tokens
            LoadTokens();

            // Ensure certificate is set up
            EnsureCertificateExists();
        }

        /// <summary>
        /// Get a valid access token, refreshing if necessary or initiating auth flow
        /// </summary>
        public async Task<string> GetAccessTokenAsync()
        {
            // If we have a valid token, return it
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _accessTokenExpiry)
            {
                Log.Information("Using existing access token");
                return _accessToken;
            }

            // If we have a refresh token, try to use it
            if (!string.IsNullOrEmpty(_refreshToken))
            {
                Log.Information("Attempting to refresh access token");
                try
                {
                    var refreshed = await RefreshAccessTokenAsync();
                    if (refreshed)
                    {
                        return _accessToken;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to refresh token");
                    // Continue to full auth flow if refresh fails
                }
            }

            // We need to do the full auth flow
            Log.Information("Starting full OAuth authentication flow");
            await StartAuthorizationFlowAsync();
            return _accessToken;
        }

        /// <summary>
        /// Start the OAuth2 PKCE authorization flow
        /// </summary>
        private async Task StartAuthorizationFlowAsync()
        {
            // Generate PKCE code verifier and challenge
            string codeVerifier = GenerateCodeVerifier();
            string codeChallenge = GenerateCodeChallenge(codeVerifier);

            // Generate state to protect against CSRF
            string state = Guid.NewGuid().ToString("N");

            // Construct the authorization URL
            var queryParams = new Dictionary<string, string>
            {
                ["response_type"] = "code",
                ["client_id"] = _clientId,
                ["redirect_uri"] = _redirectUri,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256",
                ["state"] = state,
                ["scope"] = "auth capi"  // Important: need both auth and capi scopes
            };

            string authUrl = BuildQueryString(AUTH_SERVER + AUTH_ENDPOINT, queryParams);

            // Open the default browser with the authorization URL
            OpenBrowser(authUrl);

            // Now we need to get the authorization code from the redirect URL
            string authCode = await WaitForAuthorizationCodeAsync(state);

            // Exchange the authorization code for tokens
            await ExchangeCodeForTokensAsync(authCode, codeVerifier);
        }

        /// <summary>
        /// Wait for the authorization code by starting a temporary HTTPS listener
        /// </summary>
        private async Task<string> WaitForAuthorizationCodeAsync(string expectedState)
        {
            var tcs = new TaskCompletionSource<string>();

            // Create HTTPS listener
            var listener = new HttpListener();
            listener.Prefixes.Add("https://localhost:8443/auth/");

            try
            {
                listener.Start();
                Log.Information("Waiting for authorization response on {Uri}", _redirectUri);

                // Start listening for the callback
                listener.GetContextAsync().ContinueWith(async (contextTask) =>
                {
                    HttpListenerContext context = null;

                    try
                    {
                        context = await contextTask;
                        var request = context.Request;
                        var response = context.Response;

                        // Parse the query parameters
                        var query = ParseQueryString(request.Url.Query);

                        if (query.ContainsKey("error"))
                        {
                            var errorDesc = query["error_description"];
                            Log.Error("Authorization failed: {Error}", errorDesc);
                            tcs.SetException(new Exception($"Authorization error: {errorDesc}"));

                            // Send an error response
                            string responseHtml = "<html><body><h1>Authentication Failed</h1><p>Please check the application logs for details.</p></body></html>";
                            byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                            response.ContentLength64 = buffer.Length;
                            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                            response.Close();
                        }
                        else if (query.ContainsKey("code") && query.ContainsKey("state"))
                        {
                            string receivedState = query["state"];
                            string code = query["code"];

                            // Verify state to prevent CSRF
                            if (receivedState == expectedState)
                            {
                                // Send a success response
                                string responseHtml = "<html><body><h1>Authentication Successful</h1><p>You can close this window now.</p></body></html>";
                                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                                response.ContentLength64 = buffer.Length;
                                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                response.Close();

                                Log.Information("Received authorization code");
                                tcs.SetResult(code);
                            }
                            else
                            {
                                Log.Error("State verification failed: Expected {Expected}, got {Received}", expectedState, receivedState);
                                tcs.SetException(new Exception("State verification failed"));

                                // Send an error response
                                string responseHtml = "<html><body><h1>Authentication Failed</h1><p>Invalid state parameter.</p></body></html>";
                                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                                response.ContentLength64 = buffer.Length;
                                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                response.Close();
                            }
                        }
                        else
                        {
                            Log.Error("Required parameters missing from callback");
                            tcs.SetException(new Exception("Required parameters missing from callback"));

                            // Send an error response
                            string responseHtml = "<html><body><h1>Authentication Failed</h1><p>Required parameters missing.</p></body></html>";
                            byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                            response.ContentLength64 = buffer.Length;
                            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                            response.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing authorization callback");
                        tcs.SetException(ex);

                        // Try to send an error response if possible
                        try
                        {
                            if (context != null)
                            {
                                string responseHtml = "<html><body><h1>Error</h1><p>An error occurred while processing the authentication response.</p></body></html>";
                                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                                context.Response.ContentLength64 = buffer.Length;
                                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                context.Response.Close();
                            }
                        }
                        catch
                        {
                            // Ignore errors in error handling
                        }
                    }
                    finally
                    {
                        listener.Stop();
                    }
                });

                // Wait for the task to complete with a timeout
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(5)));

                if (completedTask != tcs.Task)
                {
                    listener.Stop();
                    throw new TimeoutException("Authorization timed out after 5 minutes");
                }

                return await tcs.Task;
            }
            catch (HttpListenerException ex)
            {
                Log.Error(ex, "Failed to start HTTP listener. Make sure the certificate is properly installed and port 8443 is available");
                throw new Exception("Failed to start HTTP listener. Make sure the certificate is properly installed and port 8443 is available.", ex);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error waiting for authorization code");
                throw;
            }
        }

        /// <summary>
        /// Exchange the authorization code for access and refresh tokens
        /// </summary>
        private async Task ExchangeCodeForTokensAsync(string code, string codeVerifier)
        {
            var tokenRequestParams = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _clientId,
                ["code"] = code,
                ["redirect_uri"] = _redirectUri,
                ["code_verifier"] = codeVerifier
            };

            var content = new FormUrlEncodedContent(tokenRequestParams);

            try
            {
                var response = await _httpClient.PostAsync(AUTH_SERVER + TOKEN_ENDPOINT, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);

                // Store the tokens
                _accessToken = tokenResponse.AccessToken;
                _refreshToken = tokenResponse.RefreshToken;
                _accessTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                // Save tokens to storage
                SaveTokens();

                Log.Information("Successfully obtained access token");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to exchange code for tokens");
                throw;
            }
        }

        /// <summary>
        /// Refresh the access token using a refresh token
        /// </summary>
        private async Task<bool> RefreshAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(_refreshToken))
            {
                return false;
            }

            var refreshParams = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _clientId,
                ["refresh_token"] = _refreshToken
            };

            var content = new FormUrlEncodedContent(refreshParams);

            try
            {
                var response = await _httpClient.PostAsync(AUTH_SERVER + TOKEN_ENDPOINT, content);

                if (!response.IsSuccessStatusCode)
                {
                    // If refresh fails, we need to re-authorize
                    Log.Warning("Refresh token rejected: {StatusCode}", response.StatusCode);
                    return false;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);

                // Store the new tokens
                _accessToken = tokenResponse.AccessToken;

                // Sometimes refresh token is not included in response, only update if it's present
                if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    _refreshToken = tokenResponse.RefreshToken;
                }

                _accessTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                // Save tokens to storage
                SaveTokens();

                Log.Information("Successfully refreshed access token");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to refresh access token");
                return false;
            }
        }

        /// <summary>
        /// Save tokens to local storage
        /// </summary>
        private void SaveTokens()
        {
            var tokenData = new TokenStorage
            {
                AccessToken = _accessToken,
                RefreshToken = _refreshToken,
                ExpiresAt = _accessTokenExpiry
            };

            string json = JsonSerializer.Serialize(tokenData);
            File.WriteAllText(_tokenStoragePath, json);
        }

        /// <summary>
        /// Load tokens from local storage
        /// </summary>
        private void LoadTokens()
        {
            try
            {
                if (File.Exists(_tokenStoragePath))
                {
                    string json = File.ReadAllText(_tokenStoragePath);
                    var tokenData = JsonSerializer.Deserialize<TokenStorage>(json);

                    _accessToken = tokenData.AccessToken;
                    _refreshToken = tokenData.RefreshToken;
                    _accessTokenExpiry = tokenData.ExpiresAt;

                    Log.Information("Loaded tokens from storage");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load tokens from storage");
                // Continue with empty tokens, we'll generate new ones
            }
        }

        /// <summary>
        /// Generate a random code verifier for PKCE
        /// </summary>
        private string GenerateCodeVerifier()
        {
            const string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
            var random = new Random();
            var chars = new char[128]; // Use max allowed length

            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = allowedChars[random.Next(allowedChars.Length)];
            }

            return new string(chars);
        }

        /// <summary>
        /// Generate a code challenge from the code verifier
        /// </summary>
        private string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                // Hash the code verifier
                var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));

                // Base64 URL encode the hash
                return Base64UrlEncode(challengeBytes);
            }
        }

        /// <summary>
        /// Base64 URL encode a byte array
        /// </summary>
        private string Base64UrlEncode(byte[] input)
        {
            var base64 = Convert.ToBase64String(input);

            // Make base64 URL safe
            return base64
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>
        /// Builds a URL with query parameters
        /// </summary>
        private string BuildQueryString(string baseUrl, Dictionary<string, string> parameters)
        {
            var queryString = string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
            return baseUrl + (baseUrl.Contains("?") ? "&" : "?") + queryString;
        }

        /// <summary>
        /// Parse query string into a dictionary
        /// </summary>
        private Dictionary<string, string> ParseQueryString(string queryString)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(queryString) || queryString == "?")
                return result;

            // Remove the leading ? if present
            if (queryString.StartsWith("?"))
                queryString = queryString.Substring(1);

            var parts = queryString.Split('&');
            foreach (var part in parts)
            {
                var keyValue = part.Split('=', 2);
                if (keyValue.Length == 2)
                {
                    var key = Uri.UnescapeDataString(keyValue[0]);
                    var value = Uri.UnescapeDataString(keyValue[1]);
                    result[key] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Open the default browser with the specified URL
        /// </summary>
        private void OpenBrowser(string url)
        {
            try
            {
                Log.Information("Opening browser for authentication: {Url}", url);

                // Try to use the default OS method
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Fallback methods for different platforms
                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}"));
                }
                else if (OperatingSystem.IsLinux())
                {
                    Process.Start("xdg-open", url);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start("open", url);
                }
                else
                {
                    Log.Error("Could not open browser automatically. Please manually navigate to: {Url}", url);
                    throw;
                }
            }
        }

        /// <summary>
        /// Ensure a self-signed certificate exists for HTTPS
        /// </summary>
        private void EnsureCertificateExists()
        {
            // Certificate store path
            string certStorePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EliteInfoPanel",
                "localhost.pfx");

            // Check if certificate already exists
            if (File.Exists(certStorePath))
            {
                try
                {
                    // Try to load the certificate to verify it's valid
                    var cert = new X509Certificate2(certStorePath, "eliteinfopanel");

                    // Check if certificate is still valid
                    if (cert.NotAfter > DateTime.Now)
                    {
                        Log.Information("Self-signed certificate is valid until {ExpiryDate}", cert.NotAfter);
                        return;
                    }

                    Log.Warning("Certificate has expired, generating a new one");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error loading certificate, generating a new one");
                }
            }

            // Certificate doesn't exist or is invalid, create a new one
            try
            {
                // Generate a self-signed certificate
                Log.Information("Generating new self-signed certificate for HTTPS");

                // Check if we're running as administrator
                bool isAdmin = false;
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }

                if (!isAdmin)
                {
                    Log.Warning("Not running as administrator. You may need to manually bind the certificate to port 8443");
                }

                // Create certificate using PowerShell (on Windows)
                if (OperatingSystem.IsWindows())
                {
                    CreateCertificateWithPowerShell(certStorePath);
                }
                else
                {
                    Log.Error("Automatic certificate creation is only supported on Windows. Please create a self-signed certificate manually");
                    throw new PlatformNotSupportedException("Automatic certificate creation is only supported on Windows");
                }

                // Set up the HTTPS certificate binding
                if (isAdmin && OperatingSystem.IsWindows())
                {
                    BindCertificateToPort(certStorePath);
                }
                else
                {
                    Log.Warning("To complete setup, run the following command as administrator:");
                    Log.Warning("netsh http add sslcert ipport=0.0.0.0:8443 certhash={thumbprint} appid={{guid}}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create self-signed certificate");
                throw new Exception("Failed to create self-signed certificate", ex);
            }
        }

        /// <summary>
        /// Create a self-signed certificate using PowerShell
        /// </summary>
        private void CreateCertificateWithPowerShell(string certPath)
        {
            var scriptPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EliteInfoPanel",
                "create_cert.ps1");

            // PowerShell script to create a self-signed certificate
            string script = @"
$cert = New-SelfSignedCertificate -DnsName localhost -CertStoreLocation Cert:\LocalMachine\My -NotAfter (Get-Date).AddYears(5) -KeyAlgorithm RSA -KeyLength 2048
$password = ConvertTo-SecureString -String ""eliteinfopanel"" -Force -AsPlainText
$certPath = Join-Path -Path $env:APPDATA -ChildPath ""EliteInfoPanel\localhost.pfx""
Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $password
Write-Output $cert.Thumbprint
";

            // Write the script to a file
            File.WriteAllText(scriptPath, script);

            // Execute PowerShell script as administrator
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = true,
                Verb = "runas",
                RedirectStandardOutput = false
            };

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"PowerShell script exited with code {process.ExitCode}");
                }
            }

            // Clean up
            try
            {
                File.Delete(scriptPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Bind the certificate to port 8443
        /// </summary>
        private void BindCertificateToPort(string certPath)
        {
            // Load the certificate to get its thumbprint
            var cert = new X509Certificate2(certPath, "eliteinfopanel");
            string thumbprint = cert.Thumbprint;
            string appId = "{" + Guid.NewGuid().ToString() + "}";

            // Create the netsh command to bind the certificate
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"http add sslcert ipport=0.0.0.0:8443 certhash={thumbprint} appid={appId}",
                UseShellExecute = true,
                Verb = "runas",
                RedirectStandardOutput = false
            };

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Log.Error("Failed to bind certificate to port 8443. Exit code: {ExitCode}", process.ExitCode);
                    throw new Exception($"Failed to bind certificate to port 8443. Exit code: {process.ExitCode}");
                }
            }

            Log.Information("Successfully bound certificate to port 8443");
        }

        /// <summary>
        /// Token response model
        /// </summary>
        private class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; }

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }

        /// <summary>
        /// Token storage model
        /// </summary>
        private class TokenStorage
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}
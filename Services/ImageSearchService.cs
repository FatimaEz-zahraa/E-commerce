using System.Net.Http.Headers;
using System.Text.Json;

namespace E_commerce.Services
{
    public class ImageSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public ImageSearchService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["Pexels:ApiKey"]!;
        }

        public async Task<string?> GetProductImageUrlAsync(
            string name,
            string description,
            string category)
        {
            var query = $"{name} {category} product photo";
            var url = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(query)}&per_page=1";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue(_apiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var photos = doc.RootElement.GetProperty("photos");
            if (photos.GetArrayLength() == 0)
                return null;

            return photos[0]
                .GetProperty("src")
                .GetProperty("medium")
                .GetString();
        }
    }
}

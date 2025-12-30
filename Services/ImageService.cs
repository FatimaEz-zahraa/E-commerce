using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace E_commerce.Services;
public class ImageService
{
    private readonly HttpClient _httpClient;
    private readonly string _pexelsApiKey;

    public ImageService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _pexelsApiKey = config["Pexels:ApiKey"]!;
        _httpClient.DefaultRequestHeaders.Add("Authorization", _pexelsApiKey);
    }

    public async Task<string?> GetProductImageUrlAsync(
        string name,
        string description,
        string category,
        string brand = "")
    {
        var query = $"{brand} {name} {category} product";

        var url =
            $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(query)}&per_page=1";

        var response = await _httpClient.GetFromJsonAsync<PexelsResponse>(url);

        return response?.photos?.FirstOrDefault()?.src?.medium;
    }

    public async Task<string> GetImageAsync(string query)
    {
        int retries = 3;
        while (retries > 0)
        {
            var response = await _httpClient.GetAsync($"https://api.pexels.com/v1/search?query={query}&per_page=1");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await Task.Delay(1000); // attendre 1s et réessayer
                retries--;
                continue;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var photos = JsonDocument.Parse(json).RootElement.GetProperty("photos");

            if (photos.GetArrayLength() > 0)
                return photos[0].GetProperty("src").GetProperty("original").GetString();

            return null; // aucun résultat trouvé
        }

        return null; // après 3 retries toujours 429
    }


    private class PexelsResponse
    {
        public List<PexelsPhoto>? photos { get; set; }
    }

    private class PexelsPhoto
    {
        public PexelsSrc? src { get; set; }
    }

    private class PexelsSrc
    {
        public string? medium { get; set; }
    }
}

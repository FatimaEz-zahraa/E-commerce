using System.Net;
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

    // Méthode publique pour récupérer l'image d'un produit
    public async Task<string?> GetProductImageUrlAsync(
        string name,
        string description,
        string category,
        string brand = "")
    {
        var query = $"{brand} {name} {category} product";
        return await GetImageAsync(query);
    }

    // Méthode générique avec retry pour gérer les 429
    public async Task<string?> GetImageAsync(string query)
    {
        int retries = 3;
        int delayMs = 1000; // délai initial 1s

        while (retries > 0)
        {
            var response = await _httpClient.GetAsync(
                $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(query)}&per_page=1"
            );

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Console.WriteLine("⚠️ Trop de requêtes, attente avant retry...");
                await Task.Delay(delayMs);
                retries--;
                delayMs *= 2; // Exponential backoff
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ Erreur API Pexels : {(int)response.StatusCode}");
                return null;
            }

            // On parse le JSON seulement si la requête a réussi
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<PexelsResponse>(json);

            var imageUrl = data?.photos?.FirstOrDefault()?.src?.medium;

            // Si l'URL n'est pas HTTPS, on retourne null pour ignorer
            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("https://"))
                return null;

            return imageUrl;
        }

        Console.WriteLine("❌ Échec après 3 retries, trop de requêtes.");
        return null;
    }

    // classes pour le JSON
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

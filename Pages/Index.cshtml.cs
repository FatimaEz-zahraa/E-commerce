// Pages/Index.cshtml.cs
using E_commerce.Pages.Shared;
using E_commerce.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace E_commerce.Pages
{
    public class IndexPageModel : RagPageModel
    {
        public IndexPageModel(
            IRagService ragService,
            IProductService productService,
            ILogger<IndexPageModel> logger)
            : base(ragService, productService, logger)
        {
        }

        public void OnGet()
        {
            // Page d'accueil - pas de logique spéciale nécessaire
        }

        // Les méthodes OnPostAskAssistantAsync et OnGetSuggestions 
        // sont héritées de RagPageModel
    }
}
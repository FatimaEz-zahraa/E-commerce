using E_commerce.Services.Interfaces;
using E_commerce.Models.DTOs.Requests;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/cart")]
public class CartApiController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartApiController(ICartService cartService)
    {
        _cartService = cartService;
    }

    private string? UserId =>
        User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

    // =========================
    // AJOUT AU PANIER
    // =========================
    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] AddItemRequest request)
    {
        await _cartService.AddToCartAsync(
            UserId,
            request.ProductId,
            request.Quantity
        );

        var cart = await _cartService.GetCartAsync(UserId);
        return Ok(new
        {
            success = true,
            count = cart.TotalItems
        });
    }

    // =========================
    // COMPTEUR PANIER
    // =========================
    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        var count = await _cartService.GetCartItemCountAsync(UserId);
        return Ok(new { count });
    }

    // =========================
    // MERGE APRÈS LOGIN
    // =========================
    [Authorize]
    [HttpPost("merge")]
    public async Task<IActionResult> Merge()
    {
        await _cartService.MergeCookieCartToUserAsync(UserId!);
        return Ok(new { success = true });
    }
}

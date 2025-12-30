// Controllers/CartApiController.cs
using Microsoft.AspNetCore.Mvc;
using E_commerce.Services.Interfaces;
using E_commerce.Models.DTOs;

[Route("api/[controller]")]
[ApiController]
public class CartApiController : ControllerBase
{
    private readonly IHttpCartService _cartService;

    public CartApiController(IHttpCartService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetItemCount()
    {
        var count = await _cartService.GetItemCountAsync();
        return Ok(new { success = true, count });
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddItem([FromBody] AddItemRequest request)
    {
        try
        {
            await _cartService.AddItemAsync(request.ProductId, request.Quantity);
            var count = await _cartService.GetItemCountAsync();
            return Ok(new { success = true, count });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("details")]
    public async Task<IActionResult> GetCartDetails()
    {
        var cart = await _cartService.GetCartAsync();
        return Ok(new
        {
            success = true,
            items = cart.Items.Select(i => new
            {
                i.ProductId,
                i.ProductName,
                i.Price,
                i.Quantity,
                i.TotalPrice
            }),
            subtotal = cart.Subtotal,
            shipping = cart.ShippingCost,
            tax = cart.Tax,
            total = cart.Total,
            itemCount = cart.TotalItems
        });
    }

    [HttpPost("update")]
    public async Task<IActionResult> UpdateQuantity([FromBody] UpdateQuantityRequest request)
    {
        try
        {
            await _cartService.UpdateQuantityAsync(request.ProductId, request.Quantity);
            var cart = await _cartService.GetCartAsync();

            var item = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
            return Ok(new
            {
                success = true,
                itemTotal = item?.TotalPrice ?? 0,
                subtotal = cart.Subtotal,
                shipping = cart.ShippingCost,
                tax = cart.Tax,
                total = cart.Total,
                itemCount = cart.TotalItems
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}

public class AddItemRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class UpdateQuantityRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
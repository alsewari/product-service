using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductsService.DTOs;
using ProductsService.Services;

namespace ProductsService.Controllers;

/// <summary>
/// RESTful endpoint for the product catalogue.
/// </summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductService productService, ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger         = logger;
    }

    // ── GET api/products ───────────────────────────────────────────────────────
    /// <summary>Search / list products with optional filters and pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] ProductSearchRequest request,
        CancellationToken ct)
    {
        // Guard bounds
        request.Page     = Math.Max(1, request.Page);
        request.PageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await _productService.SearchAsync(request, ct);
        return Ok(result);
    }

    // ── GET api/products/{id} ──────────────────────────────────────────────────
    /// <summary>Get a single product by its identifier.</summary>
    [HttpGet("{id:length(24)}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(string id, CancellationToken ct)
    {
        var product = await _productService.GetByIdAsync(id, ct);
        return product is null ? NotFound(new { message = $"Product '{id}' not found." }) : Ok(product);
    }

    // ── POST api/products ──────────────────────────────────────────────────────
    /// <summary>Create a new product. Requires Seller or Admin role.</summary>
    [HttpPost]
    [Authorize(Roles = "Seller,Admin")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub")
                    ?? string.Empty;

        var created = await _productService.CreateAsync(request, sellerId, ct);
        return CreatedAtAction(nameof(GetProduct), new { id = created.Id }, created);
    }

    // ── PUT api/products/{id} ──────────────────────────────────────────────────
    /// <summary>Update an existing product. Requires Seller or Admin role.</summary>
    [HttpPut("{id:length(24)}")]
    [Authorize(Roles = "Seller,Admin")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateProduct(
        string id,
        [FromBody] UpdateProductRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var updated = await _productService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound(new { message = $"Product '{id}' not found." }) : Ok(updated);
    }

    // ── DELETE api/products/{id} ───────────────────────────────────────────────
    /// <summary>Delete a product. Requires Seller or Admin role.</summary>
    [HttpDelete("{id:length(24)}")]
    [Authorize(Roles = "Seller,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteProduct(string id, CancellationToken ct)
    {
        var deleted = await _productService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound(new { message = $"Product '{id}' not found." });
    }
}

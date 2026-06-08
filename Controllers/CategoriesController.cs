using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductsService.Domain;
using ProductsService.Services;

namespace ProductsService.Controllers;

/// <summary>
/// RESTful endpoint for product categories.
/// </summary>
[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(ICategoryService categoryService, ILogger<CategoriesController> logger)
    {
        _categoryService = categoryService;
        _logger          = logger;
    }

    // ── GET api/categories ─────────────────────────────────────────────────────
    /// <summary>List all active categories.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Category>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var categories = await _categoryService.GetAllAsync(ct);
        return Ok(categories);
    }

    // ── GET api/categories/{id} ────────────────────────────────────────────────
    /// <summary>Get a single category by identifier.</summary>
    [HttpGet("{id:length(24)}")]
    [ProducesResponseType(typeof(Category), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategory(string id, CancellationToken ct)
    {
        var category = await _categoryService.GetByIdAsync(id, ct);
        return category is null
            ? NotFound(new { message = $"Category '{id}' not found." })
            : Ok(category);
    }

    // ── GET api/categories/{id}/subcategories ──────────────────────────────────
    /// <summary>Get direct child categories of a parent.</summary>
    [HttpGet("{id:length(24)}/subcategories")]
    [ProducesResponseType(typeof(List<Category>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubcategories(string id, CancellationToken ct)
    {
        var children = await _categoryService.GetByParentIdAsync(id, ct);
        return Ok(children);
    }

    // ── POST api/categories ────────────────────────────────────────────────────
    /// <summary>Create a new category. Requires Admin role.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Category), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCategory([FromBody] Category category, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var created = await _categoryService.CreateAsync(category, ct);
        return CreatedAtAction(nameof(GetCategory), new { id = created.Id }, created);
    }

    // ── PUT api/categories/{id} ────────────────────────────────────────────────
    /// <summary>Update an existing category. Requires Admin role.</summary>
    [HttpPut("{id:length(24)}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Category), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateCategory(
        string id, [FromBody] Category category, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var updated = await _categoryService.UpdateAsync(id, category, ct);
        return updated is null
            ? NotFound(new { message = $"Category '{id}' not found." })
            : Ok(updated);
    }

    // ── DELETE api/categories/{id} ─────────────────────────────────────────────
    /// <summary>Delete a category. Requires Admin role.</summary>
    [HttpDelete("{id:length(24)}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteCategory(string id, CancellationToken ct)
    {
        var deleted = await _categoryService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound(new { message = $"Category '{id}' not found." });
    }
}

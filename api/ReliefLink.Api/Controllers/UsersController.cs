using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReliefLink.Api.Auth;
using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Data;
using ReliefLink.Api.Domain;
using ReliefLink.Api.Services;

namespace ReliefLink.Api.Controllers;

/// <summary>Administrator-only user management, including creation of staff accounts.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class UsersController(AppDbContext db, IAuditService audit) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<UserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] UserRole? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Users.AsNoTracking().AsQueryable();
        if (role is not null) query = query.Where(u => u.Role == role);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.Contains(term) || u.FullName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.FullName).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<UserDto>(items.Select(UserDto.From).ToList(), page, pageSize, total));
    }

    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var passwordError = PasswordPolicy.Validate(request.Password);
        if (passwordError is not null)
        {
            ModelState.AddModelError(nameof(request.Password), passwordError);
            return ValidationProblem(ModelState);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 11),
            Phone = request.Phone?.Trim(),
            District = request.District?.Trim(),
            Role = request.Role
        };

        db.Users.Add(user);
        audit.Record(User.GetUserId(), "user.created", nameof(Domain.User), user.Id.ToString(), new { user.Role });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered.");
        }

        return CreatedAtAction(nameof(List), new { search = user.Email }, UserDto.From(user));
    }

    [HttpPatch("{id:guid}/active")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found.");

        // An administrator must not be able to lock themselves out.
        if (user.Id == User.GetUserId() && !request.IsActive)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "You cannot disable your own account.");

        user.IsActive = request.IsActive;
        audit.Record(User.GetUserId(), request.IsActive ? "user.enabled" : "user.disabled",
            nameof(Domain.User), user.Id.ToString());
        await db.SaveChangesAsync(ct);

        return Ok(UserDto.From(user));
    }
}

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

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, ITokenService tokens, IAuditService audit) : ControllerBase
{
    // Verified against when the email is unknown, so response time does not reveal which emails exist.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("not-a-real-password-1", 11);

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
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
            Role = request.Role == SelfRegisterRole.Volunteer ? UserRole.Volunteer : UserRole.Citizen
        };

        db.Users.Add(user);
        audit.Record(user.Id, "user.registered", nameof(User), user.Id.ToString(), new { user.Role });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two concurrent registrations with the same email: the unique index decides.
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered.");
        }

        var (token, expires) = tokens.Create(user);
        return StatusCode(StatusCodes.Status201Created, new AuthResponse(token, expires, UserDto.From(user)));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        var hash = user?.PasswordHash ?? DummyHash;
        var passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, hash);

        if (user is null || !passwordOk)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");

        if (!user.IsActive)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "This account has been disabled.");

        audit.Record(user.Id, "user.login", nameof(User), user.Id.ToString());
        await db.SaveChangesAsync(ct);

        var (token, expires) = tokens.Create(user);
        return Ok(new AuthResponse(token, expires, UserDto.From(user)));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = User.GetUserId();
        if (id is null) return Unauthorized();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null || !user.IsActive) return Unauthorized();

        return Ok(UserDto.From(user));
    }
}

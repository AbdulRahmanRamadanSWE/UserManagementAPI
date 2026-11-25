// Controllers/UsersController.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Data;
using UserManagementAPI.DTOs;
using UserManagementAPI.Models;

namespace UserManagementAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<UsersController> _logger;
    private readonly PasswordHasher<User> _hasher = new();

    public UsersController(AppDbContext db, ILogger<UsersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static UserResponseDto ToDto(User u) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        CreatedAt = u.CreatedAt
    };

    // GET: api/users?page=1&pageSize=20
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponseDto>>> GetAll(
        [FromQuery] PagedQuery query, CancellationToken ct)
    {
        var baseQuery = _db.Users.AsNoTracking();
        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderBy(u => u.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<UserResponseDto>(items, query.Page, query.PageSize, total));
    }

    // GET: api/users/5
    [HttpGet("{id:int:min(1)}")]
    public async Task<ActionResult<UserResponseDto>> GetById(int id, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                CreatedAt = u.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        return user is null
            ? NotFound(new { error = $"User with ID {id} was not found." })
            : Ok(user);
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<UserResponseDto>> Create(CreateUserDto dto, CancellationToken ct)
    {
        var email = NormalizeEmail(dto.Email);

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new { error = "Email is already in use." });

        var user = new User { FullName = dto.FullName.Trim(), Email = email };
        user.PasswordHash = _hasher.HashPassword(user, dto.Password);

        try
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Race condition on the unique email index.
            _logger.LogWarning(ex, "DbUpdateException while creating user");
            return Conflict(new { error = "Email is already in use." });
        }

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user));
    }

    // PUT: api/users/5
    [HttpPut("{id:int:min(1)}")]
    public async Task<ActionResult<UserResponseDto>> Update(int id, UpdateUserDto dto, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null)
            return NotFound(new { error = $"User with ID {id} was not found." });

        var email = NormalizeEmail(dto.Email);

        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != id, ct))
            return Conflict(new { error = "Email is already in use." });

        user.FullName = dto.FullName.Trim();
        user.Email = email;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "DbUpdateException while updating user {Id}", id);
            return Conflict(new { error = "Email is already in use." });
        }

        return Ok(ToDto(user));
    }

    // DELETE: api/users/5
    [HttpDelete("{id:int:min(1)}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var rows = await _db.Users.Where(u => u.Id == id).ExecuteDeleteAsync(ct);

        return rows == 0
            ? NotFound(new { error = $"User with ID {id} was not found." })
            : NoContent();
    }
}
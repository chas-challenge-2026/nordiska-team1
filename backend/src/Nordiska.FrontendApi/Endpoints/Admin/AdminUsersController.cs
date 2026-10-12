using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nordiska.FrontendApi.Authentication.Claims;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.FrontendApi.Contracts.Responses;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure.Db;

namespace Nordiska.FrontendApi.Endpoints.Admin;

/// <summary>
/// Administrative endpoints for managing internal staff and administrator accounts (RBAC).
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
[Tags("Admin - Users")]
public class AdminUsersController : ControllerBase
{
    private static readonly string[] AllowedRoles = ["Admin", "BankStaff", "Staff"];
    private readonly BankingDbContext _db;

    public AdminUsersController(BankingDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists all administrative and staff users in the system.
    /// </summary>
    /// <param name="role">Optional filter by specific role (e.g. "Admin", "BankStaff", "Staff").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of administrative users.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden if the user is not an Administrator.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdminUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AdminUserResponse>>> GetAll(
        [FromQuery] string? role,
        CancellationToken cancellationToken)
    {
        var query = _db.StaffMembers
            .AsNoTracking()
            .Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(role))
        {
            var trimmedRole = role.Trim();
            query = query.Where(s => s.Role == trimmedRole);
        }

        var staffList = await query
            .OrderBy(s => s.FullName)
            .ToListAsync(cancellationToken);

        var responses = staffList.Select(s => new AdminUserResponse(
            Id: s.Id,
            Email: s.Email,
            Name: s.FullName,
            PhoneNumber: s.PhoneNumber,
            PersonalNum: s.EmployeeNumber,
            Roles: [s.Role],
            CreatedAt: s.CreatedAt,
            IsLockedOut: !s.IsActive)).ToList();

        return Ok(responses);
    }

    /// <summary>
    /// Retrieves a specific administrative or staff user by identifier.
    /// </summary>
    /// <param name="id">User identifier.</param>
    /// <response code="200">The administrative user.</response>
    /// <response code="404">User not found or is inactive.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserResponse>> GetById(long id)
    {
        var staff = await _db.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

        if (staff == null)
        {
            return NotFound(new { message = $"Användare med id {id} hittades inte." });
        }

        return Ok(new AdminUserResponse(
            Id: staff.Id,
            Email: staff.Email,
            Name: staff.FullName,
            PhoneNumber: staff.PhoneNumber,
            PersonalNum: staff.EmployeeNumber,
            Roles: [staff.Role],
            CreatedAt: staff.CreatedAt,
            IsLockedOut: !staff.IsActive));
    }

    /// <summary>
    /// Creates a new administrator or staff member in the staff directory.
    /// </summary>
    /// <param name="request">Creation parameters including email, name, role and password.</param>
    /// <response code="201">Created administrative user.</response>
    /// <response code="400">Invalid input parameters.</response>
    /// <response code="409">Email is already registered.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserResponse>> Create([FromBody] CreateAdminUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "E-postadress är obligatorisk." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Namn är obligatoriskt." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new { message = "Lösenord måste vara minst 6 tecken." });
        }

        var roleToAssign = string.IsNullOrWhiteSpace(request.Role) ? "Admin" : request.Role.Trim();
        if (!AllowedRoles.Contains(roleToAssign, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"Ogiltig roll. Tillåtna roller är: {string.Join(", ", AllowedRoles)}" });
        }

        var cleanEmail = request.Email.Trim().ToLowerInvariant();
        var existingByEmail = await _db.StaffMembers.AnyAsync(s => s.Email == cleanEmail && s.IsActive);
        if (existingByEmail)
        {
            return Conflict(new { message = "En administratör med denna e-postadress finns redan." });
        }

        var newStaff = new StaffMember
        {
            Email = cleanEmail,
            FullName = request.Name.Trim(),
            Role = roleToAssign,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            EmployeeNumber = !string.IsNullOrWhiteSpace(request.PersonalNum)
                ? request.PersonalNum.Trim()
                : $"ADM{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100, 999)}",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var passwordHasher = new PasswordHasher<StaffMember>();
        newStaff.PasswordHash = passwordHasher.HashPassword(newStaff, request.Password);

        _db.StaffMembers.Add(newStaff);
        await _db.SaveChangesAsync();

        var createdResponse = new AdminUserResponse(
            Id: newStaff.Id,
            Email: newStaff.Email,
            Name: newStaff.FullName,
            PhoneNumber: newStaff.PhoneNumber,
            PersonalNum: newStaff.EmployeeNumber,
            Roles: [roleToAssign],
            CreatedAt: newStaff.CreatedAt,
            IsLockedOut: false);

        return CreatedAtAction(nameof(GetById), new { id = newStaff.Id }, createdResponse);
    }

    /// <summary>
    /// Updates details of an administrative user (name, phone, role).
    /// </summary>
    /// <param name="id">User identifier.</param>
    /// <param name="request">Properties to update.</param>
    /// <response code="200">Updated administrative user.</response>
    /// <response code="400">Invalid parameters or attempting to demote the last remaining administrator.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(AdminUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserResponse>> Update(long id, [FromBody] UpdateAdminUserRequest request)
    {
        var staff = await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        if (staff == null)
        {
            return NotFound(new { message = $"Användare med id {id} hittades inte." });
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            staff.FullName = request.Name.Trim();
        }

        if (request.PhoneNumber != null)
        {
            staff.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var newRole = request.Role.Trim();
            if (!AllowedRoles.Contains(newRole, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = $"Ogiltig roll. Tillåtna roller är: {string.Join(", ", AllowedRoles)}" });
            }

            // Guard against demoting the last Administrator
            if (staff.Role == "Admin" && !string.Equals(newRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var adminCount = await _db.StaffMembers.CountAsync(s => s.Role == "Admin" && s.IsActive);
                if (adminCount <= 1)
                {
                    return BadRequest(new { message = "Kan inte ta bort Admin-rollen från systemets sista administratör." });
                }
            }

            staff.Role = newRole;
        }

        staff.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new AdminUserResponse(
            Id: staff.Id,
            Email: staff.Email,
            Name: staff.FullName,
            PhoneNumber: staff.PhoneNumber,
            PersonalNum: staff.EmployeeNumber,
            Roles: [staff.Role],
            CreatedAt: staff.CreatedAt,
            IsLockedOut: !staff.IsActive));
    }

    /// <summary>
    /// Changes the password for an administrative user.
    /// </summary>
    /// <param name="id">User identifier.</param>
    /// <param name="request">New password payload.</param>
    /// <response code="200">Password updated successfully.</response>
    /// <response code="400">Password too short or invalid.</response>
    /// <response code="404">User not found.</response>
    [HttpPut("{id:long}/password")]
    [HttpPost("{id:long}/password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(long id, [FromBody] ChangeAdminPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.NewPassword) || request.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "Nytt lösenord måste vara minst 6 tecken." });
        }

        var staff = await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        if (staff == null)
        {
            return NotFound(new { message = $"Användare med id {id} hittades inte." });
        }

        var passwordHasher = new PasswordHasher<StaffMember>();
        staff.PasswordHash = passwordHasher.HashPassword(staff, request.NewPassword);
        staff.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Lösenordet har uppdaterats." });
    }

    /// <summary>
    /// Deactivates / removes an administrative or staff user.
    /// </summary>
    /// <param name="id">User identifier to delete.</param>
    /// <response code="204">User successfully deactivated.</response>
    /// <response code="400">Attempting to delete current user or the last administrator.</response>
    /// <response code="404">User not found.</response>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id)
    {
        var currentStaffId = User.GetStaffId();
        if (currentStaffId == id)
        {
            return BadRequest(new { message = "Du kan inte radera ditt eget administratörskonto." });
        }

        var staff = await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        if (staff == null)
        {
            return NotFound(new { message = $"Användare med id {id} hittades inte." });
        }

        if (staff.Role == "Admin")
        {
            var activeAdmins = await _db.StaffMembers.CountAsync(s => s.Role == "Admin" && s.IsActive);
            if (activeAdmins <= 1)
            {
                return BadRequest(new { message = "Kan inte radera systemets sista administratör." });
            }
        }

        staff.IsActive = false;
        staff.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
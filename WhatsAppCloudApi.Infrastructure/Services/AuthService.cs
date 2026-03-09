using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;

    public AuthService(ApplicationDbContext dbContext, IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResultDto> RegisterCompanyAsync(RegisterCompanyRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.AdminEmail) ||
            string.IsNullOrWhiteSpace(request.AdminFullName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Company name, admin info, and password are required.");
        }

        var normalizedEmail = request.AdminEmail.Trim().ToLowerInvariant();

        var emailExists = await _dbContext.CompanyUsers
            .AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException("Admin email is already registered.");
        }

        var basicPlan = await _dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(x => x.IsActive && (x.Code.ToUpper() == "BASIC" || x.Name.ToUpper() == "BASIC"))
            .OrderByDescending(x => x.TrialDays)
            .FirstOrDefaultAsync(cancellationToken);

        if (basicPlan is null)
        {
            throw new InvalidOperationException("BASIC plan is not configured.");
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var trialStart = now;
                var trialEnd = trialStart.AddDays(Math.Max(basicPlan.TrialDays, 0));

                var company = new Company
                {
                    CompanyName = request.CompanyName.Trim(),
                    Email = string.IsNullOrWhiteSpace(request.CompanyEmail)
                        ? normalizedEmail
                        : request.CompanyEmail.Trim().ToLowerInvariant(),
                    Status = "ACTIVE",
                    CreatedAt = now,
                    TrialStartDate = trialStart,
                    TrialEndDate = trialEnd,
                    SubscriptionEndDate = trialEnd
                };
                _dbContext.Companies.Add(company);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var user = new CompanyUser
                {
                    CompanyId = company.CompanyId,
                    FullName = request.AdminFullName.Trim(),
                    Email = normalizedEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = "Admin",
                    IsActive = true,
                    CreatedAtUtc = now
                };
                _dbContext.CompanyUsers.Add(user);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var subscription = new CompanySubscription
                {
                    CompanyId = company.CompanyId,
                    SubscriptionPlanId = basicPlan.SubscriptionPlanId,
                    Status = "TRIAL",
                    TrialStartDate = trialStart,
                    TrialEndDate = trialEnd,
                    IsActive = true,
                    CreatedAtUtc = now
                };
                _dbContext.CompanySubscriptions.Add(subscription);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var tokens = IssueTokens(user, now);

                await tx.CommitAsync(cancellationToken);

                return new AuthResultDto
                {
                    UserId = user.CompanyUserId,
                    CompanyId = company.CompanyId,
                    Role = user.Role,
                    Permissions = user.EffectivePermissions,
                    Tokens = tokens
                };
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Email and password are required.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.CompanyUsers
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var companyIsActive = await _dbContext.Companies
            .AnyAsync(
                x => x.CompanyId == user.CompanyId &&
                    (x.Status == null || x.Status == "ACTIVE"),
                cancellationToken);
        if (!companyIsActive)
        {
            throw new UnauthorizedAccessException("Company is inactive.");
        }

        var now = DateTime.UtcNow;
        var tokens = IssueTokens(user, now);

        return new AuthResultDto
        {
            UserId = user.CompanyUserId,
            CompanyId = user.CompanyId,
            Role = user.Role,
            Permissions = user.EffectivePermissions,
            Tokens = tokens
        };
    }

    public async Task<AuthResultDto> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new InvalidOperationException("Refresh token is required.");
        }

        var principal = ValidateRefreshToken(request.RefreshToken);
        var userIdClaim = principal.FindFirstValue("UserId");
        var companyIdClaim = principal.FindFirstValue("CompanyId");

        if (!int.TryParse(userIdClaim, out var userId) || !int.TryParse(companyIdClaim, out var companyId))
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var user = await _dbContext.CompanyUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.CompanyUserId == userId && x.CompanyId == companyId && x.IsActive,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var companyIsActive = await _dbContext.Companies
            .AnyAsync(
                x => x.CompanyId == user.CompanyId &&
                    (x.Status == null || x.Status == "ACTIVE"),
                cancellationToken);
        if (!companyIsActive)
        {
            throw new UnauthorizedAccessException("Company is inactive.");
        }

        var now = DateTime.UtcNow;
        var tokens = IssueTokens(user, now);

        return new AuthResultDto
        {
            UserId = user.CompanyUserId,
            CompanyId = user.CompanyId,
            Role = user.Role,
            Permissions = user.EffectivePermissions,
            Tokens = tokens
        };
    }

    private AuthTokensDto IssueTokens(CompanyUser user, DateTime now)
    {
        var accessTokenExpiresAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var refreshTokenExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

        var accessToken = CreateSignedToken(user, now, accessTokenExpiresAt, "access");
        var refreshToken = CreateSignedToken(user, now, refreshTokenExpiresAt, "refresh");

        return new AuthTokensDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAt
        };
    }

    private string CreateSignedToken(CompanyUser user, DateTime notBefore, DateTime expiresAt, string tokenType)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("UserId", user.CompanyUserId.ToString()),
            new("CompanyId", user.CompanyId.ToString()),
            new("Role", user.Role),
            new("token_type", tokenType),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            _jwtOptions.Issuer,
            _jwtOptions.Audience,
            claims,
            notBefore: notBefore,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal ValidateRefreshToken(string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidAudience = _jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        try
        {
            var principal = handler.ValidateToken(refreshToken, validationParameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwt)
            {
                throw new UnauthorizedAccessException("Invalid refresh token.");
            }

            if (!jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Invalid refresh token.");
            }

            var tokenType = principal.FindFirstValue("token_type");
            if (!string.Equals(tokenType, "refresh", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Invalid refresh token.");
            }

            return principal;
        }
        catch (Exception)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AuthService> _logger;

    public AuthService(ApplicationDbContext dbContext, IOptions<JwtOptions> jwtOptions, ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
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
                    FullName = user.FullName,
                    CompanyName = company.CompanyName,
                    Role = user.Role,
                    IsSuperAdmin = user.IsSuperAdmin,
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

        var companyName = await _dbContext.Companies
            .Where(c => c.CompanyId == user.CompanyId)
            .Select(c => c.CompanyName)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        return new AuthResultDto
        {
            UserId = user.CompanyUserId,
            CompanyId = user.CompanyId,
            FullName = user.FullName,
            CompanyName = companyName,
            Role = user.Role,
            IsSuperAdmin = user.IsSuperAdmin,
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

        var companyName = await _dbContext.Companies
            .Where(c => c.CompanyId == user.CompanyId)
            .Select(c => c.CompanyName)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        return new AuthResultDto
        {
            UserId = user.CompanyUserId,
            CompanyId = user.CompanyId,
            FullName = user.FullName,
            CompanyName = companyName,
            Role = user.Role,
            IsSuperAdmin = user.IsSuperAdmin,
            Permissions = user.EffectivePermissions,
            Tokens = tokens
        };
    }

    // ─── Forgot Password / OTP ─────────────────────────────────────

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("Email is required.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.CompanyUsers
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive, cancellationToken);

        if (user is null)
        {
            // Don't reveal whether email exists — silently return
            return;
        }

        // Generate 6-digit OTP
        var otp = Random.Shared.Next(100000, 999999).ToString();
        user.PasswordResetOtp = BCrypt.Net.BCrypt.HashPassword(otp);
        user.PasswordResetOtpExpiryUtc = DateTime.UtcNow.AddMinutes(10);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // TODO: Replace with real email service — for now log to console
        Console.WriteLine($"[OTP] Password reset OTP for {email}: {otp}");
        _logger.LogInformation("[OTP] Password reset OTP for {Email}: {Otp}", email, otp);
    }

    public async Task<bool> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
            throw new InvalidOperationException("Email and OTP are required.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.CompanyUsers
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive, cancellationToken);

        if (user is null || string.IsNullOrEmpty(user.PasswordResetOtp))
            throw new InvalidOperationException("Invalid or expired OTP.");

        if (user.PasswordResetOtpExpiryUtc.HasValue && user.PasswordResetOtpExpiryUtc.Value < DateTime.UtcNow)
            throw new InvalidOperationException("OTP has expired. Please request a new one.");

        if (!BCrypt.Net.BCrypt.Verify(request.Otp.Trim(), user.PasswordResetOtp))
            throw new InvalidOperationException("Invalid OTP code.");

        return true;
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp) || string.IsNullOrWhiteSpace(request.NewPassword))
            throw new InvalidOperationException("Email, OTP, and new password are required.");

        if (request.NewPassword.Length < 6)
            throw new InvalidOperationException("Password must be at least 6 characters.");

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.CompanyUsers
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive, cancellationToken);

        if (user is null || string.IsNullOrEmpty(user.PasswordResetOtp))
            throw new InvalidOperationException("Invalid or expired OTP.");

        if (user.PasswordResetOtpExpiryUtc.HasValue && user.PasswordResetOtpExpiryUtc.Value < DateTime.UtcNow)
            throw new InvalidOperationException("OTP has expired. Please request a new one.");

        if (!BCrypt.Net.BCrypt.Verify(request.Otp.Trim(), user.PasswordResetOtp))
            throw new InvalidOperationException("Invalid OTP code.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetOtp = null;
        user.PasswordResetOtpExpiryUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
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

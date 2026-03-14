using System.IdentityModel.Tokens.Jwt;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WhatsAppCloudApi.Application.Exceptions;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private const int MinimumPasswordLength = 10;
    private const int TwoFactorCodeDigits = 6;
    private const int TwoFactorStepSeconds = 30;
    private const string TwoFactorIssuer = "BotGlobal";
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private readonly ApplicationDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthService> _logger;
    private readonly IDataProtector _twoFactorProtector;

    public AuthService(
        ApplicationDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        IEmailSender emailSender,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _emailSender = emailSender;
        _twoFactorProtector = dataProtectionProvider.CreateProtector("WhatsAppCloudApi.Auth.TwoFactor.v1");
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

        EnsureStrongPassword(request.Password);

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

                _dbContext.CompanyRoutingSettings.Add(new CompanyRoutingSettings
                {
                    CompanyId = company.CompanyId,
                    AssignmentMode = "MANUAL",
                    AutoAssignmentStrategy = "ROUND_ROBIN",
                    RespectExistingContactOwner = true,
                    ReassignWhenOwnerInactive = true,
                    ManualReassignmentUpdatesContactOwner = false,
                    CreatedAtUtc = now
                });

                _dbContext.CompanyUserRoutingSettings.Add(new CompanyUserRoutingSettings
                {
                    CompanyId = company.CompanyId,
                    CompanyUserId = user.CompanyUserId,
                    CanReceiveManualAssignments = true,
                    CanReceiveAutoAssignments = true,
                    CreatedAtUtc = now
                });

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
                await _dbContext.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);

                return new AuthResultDto
                {
                    UserId = user.CompanyUserId,
                    CompanyId = company.CompanyId,
                    FullName = user.FullName,
                    CompanyName = company.CompanyName,
                    Role = user.Role,
                    IsSuperAdmin = user.IsSuperAdmin,
                    TwoFactorEnabled = user.TwoFactorEnabled,
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
            _logger.LogWarning("Login failed for {Email}.", email);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var companyIsActive = await _dbContext.Companies
            .AnyAsync(
                x => x.CompanyId == user.CompanyId &&
                    !x.IsDeleted &&
                    (x.Status == null || x.Status == "ACTIVE"),
                cancellationToken);
        if (!companyIsActive)
        {
            throw new UnauthorizedAccessException("Company is inactive.");
        }

        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
            {
                throw new InvalidOperationException("Two-factor code is required for this account.");
            }

            if (!VerifyTwoFactorCode(user, request.TwoFactorCode))
            {
                throw new UnauthorizedAccessException("Invalid two-factor code.");
            }
        }

        var now = DateTime.UtcNow;
        var tokens = IssueTokens(user, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
            TwoFactorEnabled = user.TwoFactorEnabled,
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
            .AsTracking()
            .FirstOrDefaultAsync(
                x => x.CompanyUserId == userId && x.CompanyId == companyId && x.IsActive,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (user.RefreshTokenExpiryUtc is null || user.RefreshTokenExpiryUtc < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired refresh token used for user {UserId}.", userId);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (string.IsNullOrWhiteSpace(user.RefreshToken) || !BCrypt.Net.BCrypt.Verify(request.RefreshToken, user.RefreshToken))
        {
            _logger.LogWarning("Refresh token mismatch for user {UserId}.", userId);
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var companyIsActive = await _dbContext.Companies
            .AnyAsync(
                x => x.CompanyId == user.CompanyId &&
                    !x.IsDeleted &&
                    (x.Status == null || x.Status == "ACTIVE"),
                cancellationToken);
        if (!companyIsActive)
        {
            throw new UnauthorizedAccessException("Company is inactive.");
        }

        var now = DateTime.UtcNow;
        var tokens = IssueTokens(user, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
            TwoFactorEnabled = user.TwoFactorEnabled,
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
        var otpLifetime = TimeSpan.FromMinutes(10);
        var now = DateTime.UtcNow;
        var otp = RandomNumberGenerator.GetInt32(100000, 1_000_000).ToString();
        user.PasswordResetOtp = BCrypt.Net.BCrypt.HashPassword(otp);
        user.PasswordResetOtpExpiryUtc = now.Add(otpLifetime);
        user.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // TODO: Replace with real email service — for now log to console
        try
        {
            await _emailSender.SendPasswordResetOtpAsync(user.Email, user.FullName, otp, otpLifetime, user.CompanyId, cancellationToken);
            _logger.LogInformation("Password reset OTP sent to {Email}.", email);
        }
        catch (Exception ex)
        {
            await TryClearPasswordResetOtpAsync(user, cancellationToken);
            _logger.LogError(ex, "Password reset OTP delivery failed for {Email}.", email);

            if (ex is EmailDeliveryException)
            {
                throw;
            }

            throw new EmailDeliveryException("Password reset email could not be sent. Please try again later.", ex);
        }
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

        EnsureStrongPassword(request.NewPassword);

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
        user.RefreshToken = null;
        user.RefreshTokenExpiryUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TwoFactorStatusDto> GetTwoFactorStatusAsync(int companyId, int userId, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredActiveUserAsync(companyId, userId, cancellationToken);
        return ToTwoFactorStatus(user);
    }

    public async Task<TwoFactorSetupDto> BeginTwoFactorSetupAsync(int companyId, int userId, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredActiveUserAsync(companyId, userId, cancellationToken);
        if (user.TwoFactorEnabled)
        {
            throw new InvalidOperationException("Two-factor authentication is already enabled. Disable it first to reconfigure.");
        }

        var secretBytes = RandomNumberGenerator.GetBytes(20);
        var manualEntryKey = Base32Encode(secretBytes);
        user.TwoFactorSecretProtected = _twoFactorProtector.Protect(manualEntryKey);
        user.TwoFactorUpdatedAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var accountName = user.Email;
        var otpAuthUri = BuildOtpAuthUri(TwoFactorIssuer, accountName, manualEntryKey);

        return new TwoFactorSetupDto
        {
            IsEnabled = false,
            Issuer = TwoFactorIssuer,
            AccountName = accountName,
            ManualEntryKey = manualEntryKey,
            OtpAuthUri = otpAuthUri
        };
    }

    public async Task<TwoFactorStatusDto> ActivateTwoFactorAsync(
        int companyId,
        int userId,
        TwoFactorActivateRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredActiveUserAsync(companyId, userId, cancellationToken);

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecretProtected))
        {
            throw new InvalidOperationException("Two-factor setup has not been started yet.");
        }

        var secret = UnprotectTwoFactorSecret(user.TwoFactorSecretProtected);
        if (!VerifyTotp(secret, request.Code))
        {
            throw new InvalidOperationException("Invalid authenticator code.");
        }

        var now = DateTime.UtcNow;
        user.TwoFactorEnabled = true;
        user.TwoFactorEnabledAtUtc = now;
        user.TwoFactorUpdatedAtUtc = now;
        user.UpdatedAtUtc = now;
        user.RefreshToken = null;
        user.RefreshTokenExpiryUtc = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToTwoFactorStatus(user);
    }

    public async Task<TwoFactorStatusDto> DeactivateTwoFactorAsync(int companyId, int userId, CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredActiveUserAsync(companyId, userId, cancellationToken);

        var now = DateTime.UtcNow;
        user.TwoFactorEnabled = false;
        user.TwoFactorSecretProtected = null;
        user.TwoFactorEnabledAtUtc = null;
        user.TwoFactorUpdatedAtUtc = now;
        user.UpdatedAtUtc = now;
        user.RefreshToken = null;
        user.RefreshTokenExpiryUtc = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToTwoFactorStatus(user);
    }

    private AuthTokensDto IssueTokens(CompanyUser user, DateTime now)
    {
        var accessTokenExpiresAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var refreshTokenExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

        var accessToken = CreateSignedToken(user, now, accessTokenExpiresAt, "access");
        var refreshToken = CreateSignedToken(user, now, refreshTokenExpiresAt, "refresh");

        user.RefreshToken = BCrypt.Net.BCrypt.HashPassword(refreshToken);
        user.RefreshTokenExpiryUtc = refreshTokenExpiresAt;
        user.UpdatedAtUtc = now;

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
            new(JwtRegisteredClaimNames.Sub, user.CompanyUserId.ToString()),
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
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
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

    private async Task<CompanyUser> GetRequiredActiveUserAsync(int companyId, int userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.CompanyUsers
            .AsTracking()
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.CompanyUserId == userId && x.IsActive,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("User is inactive or does not exist.");
        }

        return user;
    }

    private TwoFactorStatusDto ToTwoFactorStatus(CompanyUser user)
    {
        return new TwoFactorStatusDto
        {
            IsEnabled = user.TwoFactorEnabled,
            EnabledAtUtc = user.TwoFactorEnabledAtUtc,
            UpdatedAtUtc = user.TwoFactorUpdatedAtUtc
        };
    }

    private bool VerifyTwoFactorCode(CompanyUser user, string code)
    {
        if (string.IsNullOrWhiteSpace(user.TwoFactorSecretProtected))
        {
            throw new InvalidOperationException("Two-factor authentication is enabled but no secret is configured.");
        }

        var secret = UnprotectTwoFactorSecret(user.TwoFactorSecretProtected);
        return VerifyTotp(secret, code);
    }

    private string UnprotectTwoFactorSecret(string protectedSecret)
    {
        try
        {
            return _twoFactorProtector.Unprotect(protectedSecret);
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Two-factor secret could not be decrypted. Please disable and re-enable two-factor authentication.",
                ex);
        }
    }

    private static bool VerifyTotp(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalizedCode = new string(code.Where(char.IsDigit).ToArray());
        if (normalizedCode.Length != TwoFactorCodeDigits)
        {
            return false;
        }

        byte[] secretBytes;
        try
        {
            secretBytes = Base32Decode(secret);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        var nowStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TwoFactorStepSeconds;
        for (long stepOffset = -1; stepOffset <= 1; stepOffset++)
        {
            var expected = ComputeTotp(secretBytes, nowStep + stepOffset, TwoFactorCodeDigits);
            if (FixedTimeEquals(expected, normalizedCode))
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeTotp(byte[] secretBytes, long timestep, int digits)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, timestep);

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0F;

        var binaryCode =
            ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        var otp = binaryCode % (int)Math.Pow(10, digits);
        return otp.ToString(new string('0', digits), CultureInfo.InvariantCulture);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string BuildOtpAuthUri(string issuer, string accountName, string secret)
    {
        var encodedLabel = Uri.EscapeDataString($"{issuer}:{accountName}");
        var encodedIssuer = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{encodedLabel}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits={TwoFactorCodeDigits}&period={TwoFactorStepSeconds}";
    }

    private static string Base32Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var output = new StringBuilder((int)Math.Ceiling(bytes.Length / 5d) * 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                var index = (buffer >> (bitsLeft - 5)) & 31;
                output.Append(Base32Alphabet[index]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            var index = (buffer << (5 - bitsLeft)) & 31;
            output.Append(Base32Alphabet[index]);
        }

        return output.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var cleaned = input.Trim().TrimEnd('=').Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var result = new List<byte>(cleaned.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in cleaned)
        {
            var index = Base32Alphabet.IndexOf(c);
            if (index < 0)
            {
                throw new InvalidOperationException("Two-factor secret contains invalid Base32 characters.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                result.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        return [.. result];
    }

    private static void EnsureStrongPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumPasswordLength)
        {
            throw new InvalidOperationException($"Password must be at least {MinimumPasswordLength} characters.");
        }

        if (!password.Any(char.IsUpper)
            || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit)
            || !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            throw new InvalidOperationException("Password must include upper, lower, number, and special character.");
        }
    }

    private async Task TryClearPasswordResetOtpAsync(CompanyUser user, CancellationToken cancellationToken)
    {
        user.PasswordResetOtp = null;
        user.PasswordResetOtpExpiryUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear password reset OTP for user {UserId} after email delivery failure.", user.CompanyUserId);
        }
    }
}

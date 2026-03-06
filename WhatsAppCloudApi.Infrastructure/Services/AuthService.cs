using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Data;
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

        var basicPlan = await ResolveBasicPlanAsync(cancellationToken);
        if (basicPlan is null)
        {
            throw new InvalidOperationException("BASIC plan is not configured.");
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var trialStart = now;
        var trialEnd = trialStart.AddDays(Math.Max(basicPlan.TrialDays, 0));

        var company = new Company
        {
            CompanyName = request.CompanyName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.CompanyEmail) ? normalizedEmail : request.CompanyEmail.Trim().ToLowerInvariant(),
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

        if (!basicPlan.SubscriptionPlanId.HasValue)
        {
            throw new InvalidOperationException("Unable to resolve SubscriptionPlans primary key column. Expected one of: SubscriptionPlanId, PlanId, Id.");
        }

        var subscription = new CompanySubscription
        {
            CompanyId = company.CompanyId,
            SubscriptionPlanId = basicPlan.SubscriptionPlanId.Value,
            Status = "TRIAL",
            IsActive = true,
            TrialStartDate = trialStart,
            TrialEndDate = trialEnd,
            CreatedAtUtc = now
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var tokens = IssueTokens(user, now);
        user.RefreshToken = tokens.RefreshToken;
        user.RefreshTokenExpiryUtc = now.AddDays(_jwtOptions.RefreshTokenDays);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);

        return new AuthResultDto
        {
            UserId = user.CompanyUserId,
            CompanyId = company.CompanyId,
            Role = user.Role,
            Tokens = tokens
        };
    }

    private async Task<ResolvedBasicPlan?> ResolveBasicPlanAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = await GetTableColumnsAsync(connection, "SubscriptionPlans", cancellationToken);
        if (columns.Count == 0)
        {
            return null;
        }

        var idColumn = PickColumn(columns, "SubscriptionPlanId", "PlanId", "Id");
        var trialDaysColumn = PickColumn(columns, "TrialDays");
        var isActiveColumn = PickColumn(columns, "IsActive", "Active", "Status");
        var codeColumn = PickColumn(columns, "Code", "PlanCode");
        var nameColumn = PickColumn(columns, "Name", "PlanName");

        if (trialDaysColumn is null)
        {
            throw new InvalidOperationException("SubscriptionPlans table must contain TrialDays column.");
        }

        var whereParts = new List<string>();
        if (isActiveColumn is not null)
        {
            var activeType = columns[isActiveColumn];
            if (activeType.Equals("bit", StringComparison.OrdinalIgnoreCase) ||
                activeType.Equals("tinyint", StringComparison.OrdinalIgnoreCase) ||
                activeType.Equals("int", StringComparison.OrdinalIgnoreCase) ||
                activeType.Equals("smallint", StringComparison.OrdinalIgnoreCase))
            {
                whereParts.Add($"[{isActiveColumn}] = 1");
            }
            else
            {
                whereParts.Add($"UPPER(CAST([{isActiveColumn}] AS NVARCHAR(100))) IN ('ACTIVE','1','TRUE','YES')");
            }
        }

        var basicPredicates = new List<string>();
        if (codeColumn is not null)
        {
            basicPredicates.Add($"UPPER(CAST([{codeColumn}] AS NVARCHAR(200))) = 'BASIC'");
        }

        if (nameColumn is not null)
        {
            basicPredicates.Add($"UPPER(CAST([{nameColumn}] AS NVARCHAR(200))) = 'BASIC'");
        }

        if (basicPredicates.Count > 0)
        {
            whereParts.Add($"({string.Join(" OR ", basicPredicates)})");
        }

        var whereClause = whereParts.Count > 0 ? string.Join(" AND ", whereParts) : "1=1";
        var idSelect = idColumn is null ? "CAST(NULL AS INT) AS SubscriptionPlanId" : $"[{idColumn}] AS SubscriptionPlanId";
        var sql = $"""
            SELECT TOP 1
                {idSelect},
                [{trialDaysColumn}] AS TrialDays
            FROM [SubscriptionPlans]
            WHERE {whereClause}
            ORDER BY [{trialDaysColumn}] DESC
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var trialDays = reader["TrialDays"] is DBNull ? 0 : Convert.ToInt32(reader["TrialDays"]);
        int? subscriptionPlanId = null;
        if (reader["SubscriptionPlanId"] is not DBNull)
        {
            subscriptionPlanId = Convert.ToInt32(reader["SubscriptionPlanId"]);
        }

        return new ResolvedBasicPlan
        {
            SubscriptionPlanId = subscriptionPlanId,
            TrialDays = trialDays
        };
    }

    private static async Task<Dictionary<string, string>> GetTableColumnsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName
            """;
        var tableNameParameter = command.CreateParameter();
        tableNameParameter.ParameterName = "@tableName";
        tableNameParameter.Value = tableName;
        command.Parameters.Add(tableNameParameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(0);
            var dataType = reader.GetString(1);
            result[columnName] = dataType;
        }

        return result;
    }

    private static string? PickColumn(IReadOnlyDictionary<string, string> columns, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (columns.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private sealed class ResolvedBasicPlan
    {
        public int? SubscriptionPlanId { get; set; }
        public int TrialDays { get; set; }
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
        user.RefreshToken = tokens.RefreshToken;
        user.RefreshTokenExpiryUtc = now.AddDays(_jwtOptions.RefreshTokenDays);
        user.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResultDto
        {
            UserId = user.CompanyUserId,
            CompanyId = user.CompanyId,
            Role = user.Role,
            Tokens = tokens
        };
    }

    public async Task<AuthResultDto> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new InvalidOperationException("Refresh token is required.");
        }

        var now = DateTime.UtcNow;
        var user = await _dbContext.CompanyUsers
            .AsTracking()
            .FirstOrDefaultAsync(
                x => x.RefreshToken == request.RefreshToken && x.RefreshTokenExpiryUtc != null && x.RefreshTokenExpiryUtc >= now && x.IsActive,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var tokens = IssueTokens(user, now);
        user.RefreshToken = tokens.RefreshToken;
        user.RefreshTokenExpiryUtc = now.AddDays(_jwtOptions.RefreshTokenDays);
        user.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResultDto
        {
            UserId = user.CompanyUserId,
            CompanyId = user.CompanyId,
            Role = user.Role,
            Tokens = tokens
        };
    }

    private AuthTokensDto IssueTokens(CompanyUser user, DateTime now)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new("UserId", user.CompanyUserId.ToString()),
            new("CompanyId", user.CompanyId.ToString()),
            new("Role", user.Role),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            _jwtOptions.Issuer,
            _jwtOptions.Audience,
            claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthTokensDto
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = GenerateRefreshToken(),
            AccessTokenExpiresAtUtc = expiresAt
        };
    }

    private static string GenerateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

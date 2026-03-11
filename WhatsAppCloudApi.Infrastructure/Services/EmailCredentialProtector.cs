using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using WhatsAppCloudApi.Application.Interfaces;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class EmailCredentialProtector : IEmailCredentialProtector
{
    private readonly IDataProtector _protector;

    public EmailCredentialProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("WhatsAppCloudApi.EmailQueue.Credentials.v1");
    }

    public string Protect(string value) => _protector.Protect(value);

    public string Unprotect(string value)
    {
        try
        {
            return _protector.Unprotect(value);
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Stored SMTP credentials cannot be decrypted because the ASP.NET Data Protection key ring changed or is missing. Re-save the email account password after restoring a persistent key ring.",
                ex);
        }
    }
}

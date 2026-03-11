namespace WhatsAppCloudApi.Application.Interfaces;

public interface IEmailCredentialProtector
{
    string Protect(string value);
    string Unprotect(string value);
}

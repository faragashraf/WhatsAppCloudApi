using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Utilities;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class CustomerConversationResolver : ICustomerConversationResolver
{
    private readonly ApplicationDbContext _dbContext;

    public CustomerConversationResolver(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ResolvedConversationContext> ResolveAsync(
        int companyId,
        string phoneNumber,
        int? whatsAppPhoneNumberId,
        string? preferredContactName = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(phoneNumber)
            ?? throw new InvalidOperationException("Invalid phone number.");

        var now = DateTime.UtcNow;
        var trimmedName = string.IsNullOrWhiteSpace(preferredContactName) ? null : preferredContactName.Trim();

        var contact = await _dbContext.Contacts
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.PhoneNumber == normalizedPhoneNumber,
                cancellationToken);

        if (contact is null)
        {
            contact = new Contact
            {
                CompanyId = companyId,
                Name = trimmedName ?? normalizedPhoneNumber,
                PhoneNumber = normalizedPhoneNumber,
                Source = string.IsNullOrWhiteSpace(source) ? "crm_resolver" : source.Trim(),
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                CreatedAtUtc = now
            };

            _dbContext.Contacts.Add(contact);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var changed = false;

            if (!string.IsNullOrWhiteSpace(trimmedName)
                && (string.IsNullOrWhiteSpace(contact.Name)
                    || string.Equals(contact.Name, contact.PhoneNumber, StringComparison.Ordinal)
                    || string.Equals(contact.Name, normalizedPhoneNumber, StringComparison.Ordinal)))
            {
                contact.Name = trimmedName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(contact.Source) && !string.IsNullOrWhiteSpace(source))
            {
                contact.Source = source.Trim();
                changed = true;
            }

            contact.LastSeenAtUtc = now;
            contact.UpdatedAtUtc = now;
            changed = true;

            if (changed)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId
                    && x.ContactId == contact.ContactId
                    && x.WhatsAppPhoneNumberId == whatsAppPhoneNumberId,
                cancellationToken);

        conversation ??= await _dbContext.Conversations
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId
                    && x.ContactNumber == normalizedPhoneNumber
                    && x.WhatsAppPhoneNumberId == whatsAppPhoneNumberId,
                cancellationToken);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                CompanyId = companyId,
                ContactId = contact.ContactId,
                ContactNumber = normalizedPhoneNumber,
                ContactName = contact.Name,
                WhatsAppPhoneNumberId = whatsAppPhoneNumberId,
                Status = "OPEN",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _dbContext.Conversations.Add(conversation);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var changed = false;

            if (conversation.ContactId != contact.ContactId)
            {
                conversation.ContactId = contact.ContactId;
                changed = true;
            }

            if (!string.Equals(conversation.ContactNumber, normalizedPhoneNumber, StringComparison.Ordinal))
            {
                conversation.ContactNumber = normalizedPhoneNumber;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(contact.Name)
                && !string.Equals(conversation.ContactName, contact.Name, StringComparison.Ordinal))
            {
                conversation.ContactName = contact.Name;
                changed = true;
            }

            if (changed)
            {
                conversation.UpdatedAtUtc = now;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return new ResolvedConversationContext
        {
            NormalizedPhoneNumber = normalizedPhoneNumber,
            Contact = contact,
            Conversation = conversation
        };
    }
}

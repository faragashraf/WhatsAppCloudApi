using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/contacts")]
[Authorize]
public sealed class ContactsController : ApiControllerBase
{
    private readonly IContactService _contactService;
    private readonly ITenantContextAccessor _tenantContext;

    public ContactsController(IContactService contactService, ITenantContextAccessor tenantContext)
    {
        _contactService = contactService;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetContacts([FromQuery] ContactQueryParams query, CancellationToken ct)
        => ToActionResult(await _contactService.GetContactsAsync(_tenantContext.GetRequiredContext().CompanyId, query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetContact(long id, CancellationToken ct)
        => ToActionResult(await _contactService.GetContactByIdAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));

    [HttpPost]
    public async Task<IActionResult> CreateContact([FromBody] ContactUpsertRequest request, CancellationToken ct)
        => ToActionResult(await _contactService.CreateContactAsync(_tenantContext.GetRequiredContext().CompanyId, request, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateContact(long id, [FromBody] ContactUpsertRequest request, CancellationToken ct)
        => ToActionResult(await _contactService.UpdateContactAsync(_tenantContext.GetRequiredContext().CompanyId, id, request, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteContact(long id, CancellationToken ct)
        => ToActionResult(await _contactService.DeleteContactAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));

    [HttpPost("import")]
    public async Task<IActionResult> ImportContacts([FromBody] ContactImportRequest request, CancellationToken ct)
        => ToActionResult(await _contactService.ImportContactsAsync(_tenantContext.GetRequiredContext().CompanyId, request, ct));
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication2.Portal;

namespace WebApplication2.Controllers;

[ApiController]
[Route("api/portal")]
[Authorize]
public sealed class PortalController(IPortalService portal) : ControllerBase
{
    private string UserName => User.Identity?.Name ?? User.FindFirst("sub")?.Value ?? "unknown";

    [HttpGet("dashboard")]
    [Authorize(Policy = "PortalReader")]
    public Dashboard Dashboard() => portal.GetDashboard(DateOnly.FromDateTime(DateTime.UtcNow));

    [HttpGet("documents")]
    [Authorize(Policy = "PortalReader")]
    public IReadOnlyCollection<CompanyDocument> Documents([FromQuery] string? query, [FromQuery] string? category) => portal.GetDocuments(query, category);

    [HttpPost("documents")]
    [Authorize(Policy = "DocumentManager")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<CompanyDocument>> AddDocument([FromForm] CompanyDocumentRequest request, IFormFile file, CancellationToken cancellationToken) => Ok(await portal.AddDocumentAsync(request, file, UserName, cancellationToken));

    [HttpPost("documents/{id:guid}/review")]
    [Authorize(Policy = "DocumentReviewer")]
    public ActionResult<CompanyDocument> ReviewDocument(Guid id, ReviewRequest request) => Ok(portal.ReviewDocument(id, request, UserName));

    [HttpGet("documents/{id:guid}/download")]
    [Authorize(Policy = "PortalReader")]
    public IActionResult DownloadDocument(Guid id) { var file = portal.GetDocumentFile(id); return PhysicalFile(file.Path, "application/octet-stream", file.FileName); }

    [HttpGet("accreditations")]
    [Authorize(Policy = "PortalReader")]
    public IReadOnlyCollection<Accreditation> Accreditations() => portal.GetAccreditations();

    [HttpPost("accreditations")]
    [Authorize(Policy = "AccreditationManager")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<Accreditation>> AddAccreditation([FromForm] AccreditationRequest request, IFormFile evidence, CancellationToken cancellationToken) => Ok(await portal.AddAccreditationAsync(request, evidence, UserName, cancellationToken));

    [HttpGet("programmes")]
    [Authorize(Policy = "PortalReader")]
    public IReadOnlyCollection<Programme> Programmes() => portal.GetProgrammes();

    [HttpPost("programmes")]
    [Authorize(Policy = "ProgrammeManager")]
    public Programme AddProgramme(ProgrammeRequest request) => portal.AddProgramme(request, UserName);

    [HttpPost("programmes/{id:guid}/approve")]
    [Authorize(Policy = "AccreditationManager")]
    public Programme ApproveProgramme(Guid id) => portal.ApproveProgramme(id, UserName);

    [HttpGet("practitioners")]
    [Authorize(Policy = "PractitionerManager")]
    public IReadOnlyCollection<Practitioner> Practitioners() => portal.GetPractitioners();

    [HttpPost("practitioners")]
    [Authorize(Policy = "PractitionerManager")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<Practitioner>> AddPractitioner([FromForm] PractitionerRequest request, IFormFile cv, CancellationToken cancellationToken) => Ok(await portal.AddPractitionerAsync(request, cv, UserName, cancellationToken));

    [HttpGet("materials")]
    [Authorize(Policy = "PortalReader")]
    public IReadOnlyCollection<LearningMaterial> Materials() => portal.GetMaterials();

    [HttpPost("materials")]
    [Authorize(Policy = "MaterialManager")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<LearningMaterial>> AddMaterial([FromForm] MaterialRequest request, IFormFile file, CancellationToken cancellationToken) => Ok(await portal.AddMaterialAsync(request, file, UserName, cancellationToken));

    [HttpPost("materials/{id:guid}/review")]
    [Authorize(Policy = "DocumentReviewer")]
    public LearningMaterial ReviewMaterial(Guid id, ReviewRequest request) => portal.ReviewMaterial(id, request, UserName);

    [HttpGet("materials/{id:guid}/download")]
    [Authorize(Policy = "PortalReader")]
    public IActionResult DownloadMaterial(Guid id) { var file = portal.GetMaterialFile(id); return PhysicalFile(file.Path, "application/octet-stream", file.FileName); }

    [HttpPost("service-level-agreements")]
    [Authorize(Policy = "AgreementManager")]
    public SlaDocument GenerateSla(SlaRequest request) => portal.GenerateSla(request, UserName);

    [HttpGet("audit")]
    [Authorize(Policy = "Auditor")]
    public IReadOnlyCollection<AuditEntry> Audit() => portal.GetAudit();

    [HttpGet("accreditation-applications")]
    [Authorize(Policy = "PortalReader")]
    public IReadOnlyCollection<AccreditationApplication> AccreditationApplications() => portal.GetAccreditationApplications();

    [HttpPost("accreditation-applications")]
    [Authorize(Policy = "AccreditationManager")]
    public AccreditationApplication AddAccreditationApplication(AccreditationApplicationRequest request) => portal.AddAccreditationApplication(request, UserName);

    [HttpPost("accreditation-applications/{id:guid}/status")]
    [Authorize(Policy = "AccreditationManager")]
    public AccreditationApplication UpdateAccreditationApplication(Guid id, AccreditationApplicationStatus status) => portal.UpdateAccreditationApplication(id, status, UserName);

    [HttpGet("accreditation-applications/{id:guid}/evidence")]
    [Authorize(Policy = "PortalReader")]
    public IReadOnlyCollection<EvidenceRequirement> Evidence(Guid id) => portal.GetEvidenceRequirements(id);

    [HttpPost("accreditation-applications/{id:guid}/evidence")]
    [Authorize(Policy = "AccreditationManager")]
    public EvidenceRequirement AddEvidence(Guid id, EvidenceRequirementRequest request) => portal.AddEvidenceRequirement(id, request, UserName);

    [HttpPost("evidence/{id:guid}/review")]
    [Authorize(Policy = "DocumentReviewer")]
    public EvidenceRequirement ReviewEvidence(Guid id, EvidenceReviewRequest request) => portal.ReviewEvidenceRequirement(id, request, UserName);

    [HttpGet("notifications")]
    [Authorize(Policy = "PortalReader")]
    public IReadOnlyCollection<NotificationDelivery> Notifications() => portal.GetNotificationDeliveries();
}

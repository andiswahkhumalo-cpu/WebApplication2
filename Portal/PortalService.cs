using System.Collections.Concurrent;
using System.Text;

namespace WebApplication2.Portal;

public interface IPortalService
{
    Dashboard GetDashboard(DateOnly today);
    IReadOnlyCollection<CompanyDocument> GetDocuments(string? query, string? category);
    Task<CompanyDocument> AddDocumentAsync(CompanyDocumentRequest request, IFormFile file, string user, CancellationToken cancellationToken);
    CompanyDocument ReviewDocument(Guid id, ReviewRequest request, string user);
    (string Path, string FileName) GetDocumentFile(Guid id);
    IReadOnlyCollection<Accreditation> GetAccreditations();
    Task<Accreditation> AddAccreditationAsync(AccreditationRequest request, IFormFile evidence, string user, CancellationToken cancellationToken);
    IReadOnlyCollection<Programme> GetProgrammes();
    Programme AddProgramme(ProgrammeRequest request, string user);
    Programme ApproveProgramme(Guid id, string user);
    IReadOnlyCollection<Practitioner> GetPractitioners();
    Task<Practitioner> AddPractitionerAsync(PractitionerRequest request, IFormFile cv, string user, CancellationToken cancellationToken);
    IReadOnlyCollection<LearningMaterial> GetMaterials();
    Task<LearningMaterial> AddMaterialAsync(MaterialRequest request, IFormFile file, string user, CancellationToken cancellationToken);
    LearningMaterial ReviewMaterial(Guid id, ReviewRequest request, string user);
    (string Path, string FileName) GetMaterialFile(Guid id);
    SlaDocument GenerateSla(SlaRequest request, string user);
    IReadOnlyCollection<AuditEntry> GetAudit();
    IReadOnlyCollection<Alert> FindDueAlerts(DateOnly today);
    IReadOnlyCollection<AccreditationApplication> GetAccreditationApplications();
    AccreditationApplication AddAccreditationApplication(AccreditationApplicationRequest request, string user);
    AccreditationApplication UpdateAccreditationApplication(Guid id, AccreditationApplicationStatus status, string user);
    IReadOnlyCollection<EvidenceRequirement> GetEvidenceRequirements(Guid applicationId);
    EvidenceRequirement AddEvidenceRequirement(Guid applicationId, EvidenceRequirementRequest request, string user);
    EvidenceRequirement ReviewEvidenceRequirement(Guid id, EvidenceReviewRequest request, string user);
    IReadOnlyCollection<NotificationDelivery> GetNotificationDeliveries();
    void MarkNotification(NotificationDelivery notification, NotificationStatus status, string? error = null);
}

public interface IFileSafetyScanner { Task EnsureSafeAsync(IFormFile file, CancellationToken cancellationToken); }
public sealed class BasicFileSafetyScanner : IFileSafetyScanner
{
    private static readonly HashSet<string> Allowed = [".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".csv"];
    public async Task EnsureSafeAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > 25 * 1024 * 1024) throw new ArgumentException("Files must be between 1 byte and 25 MB.");
        if (!Allowed.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) throw new ArgumentException("Unsupported file type.");
        await using var stream = file.OpenReadStream();
        var buffer = new byte[4096];
        var read = await stream.ReadAsync(buffer, cancellationToken);
        var content = Encoding.UTF8.GetString(buffer, 0, read);
        if (content.Contains("EICAR-STANDARD-ANTIVIRUS-TEST-FILE", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Malware scan rejected the file.");
    }
}

public sealed class PortalService(IWebHostEnvironment environment, IFileSafetyScanner scanner) : IPortalService
{
    private readonly ConcurrentDictionary<Guid, CompanyDocument> documents = new();
    private readonly ConcurrentDictionary<Guid, Accreditation> accreditations = new();
    private readonly ConcurrentDictionary<Guid, Programme> programmes = new();
    private readonly ConcurrentDictionary<Guid, Practitioner> practitioners = new();
    private readonly ConcurrentDictionary<Guid, LearningMaterial> materials = new();
    private readonly ConcurrentDictionary<Guid, AccreditationApplication> applications = new();
    private readonly ConcurrentDictionary<Guid, EvidenceRequirement> evidenceRequirements = new();
    private readonly ConcurrentDictionary<Guid, NotificationDelivery> notificationDeliveries = new();
    private readonly ConcurrentBag<AuditEntry> audit = [];
    private readonly string storageRoot = Path.Combine(environment.ContentRootPath, "App_Data", "portal-files");

    public Dashboard GetDashboard(DateOnly today)
    {
        var docs = documents.Values;
        var documentLimit = today.AddDays(30);
        var accreditationLimit = today.AddDays(30);
        return new(today.ToDateTime(TimeOnly.MinValue), docs.Where(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value >= today && d.ExpiryDate.Value <= documentLimit).ToArray(), docs.Where(d => d.ExpiryDate.HasValue && d.ExpiryDate.Value < today && d.Status != RecordStatus.Superseded).ToArray(), accreditations.Values.Where(a => a.ValidTo.HasValue && a.ValidTo.Value >= today && a.ValidTo.Value <= accreditationLimit).ToArray(), docs.Where(d => d.Status == RecordStatus.Uploaded).Select(d => audit.FirstOrDefault(a => a.EntityId == d.Id && a.Action == "Uploaded")).Where(a => a is not null).ToArray()!);
    }

    public IReadOnlyCollection<CompanyDocument> GetDocuments(string? query, string? category) => documents.Values.Where(d => (string.IsNullOrWhiteSpace(query) || $"{d.Title} {d.Issuer} {d.ReferenceNumber}".Contains(query, StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrWhiteSpace(category) || d.Category.Equals(category, StringComparison.OrdinalIgnoreCase))).OrderByDescending(d => d.DocumentDate).ToArray();

    public async Task<CompanyDocument> AddDocumentAsync(CompanyDocumentRequest request, IFormFile file, string user, CancellationToken cancellationToken)
    {
        ValidateDocument(request); await scanner.EnsureSafeAsync(file, cancellationToken); var id = Guid.NewGuid(); var path = await SaveAsync(file, id, cancellationToken);
        var record = new CompanyDocument(id, request.Title, request.Category, request.Issuer, request.ReferenceNumber, request.CompanyOrSite, request.DocumentDate, DateOnly.FromDateTime(DateTime.UtcNow), request.ExpiryDate, request.CertificationDate, request.ReviewDate, request.Owner, request.AuthorisedRecipients, request.Sensitivity, 1, RecordStatus.Uploaded, Path.GetFileName(file.FileName), path, null, null);
        documents[id] = record; AddAudit(user, "Uploaded", "CompanyDocument", id, record.FileName); return record;
    }

    public CompanyDocument ReviewDocument(Guid id, ReviewRequest request, string user)
    {
        if (!documents.TryGetValue(id, out var old)) throw new KeyNotFoundException("Document not found.");
        if (old.Status is not (RecordStatus.Uploaded or RecordStatus.Returned)) throw new InvalidOperationException("Only uploaded or returned documents can be reviewed.");
        var updated = old with { Status = request.Approved ? RecordStatus.Approved : RecordStatus.Returned, ReviewComments = request.Comments, ReviewDate = DateOnly.FromDateTime(DateTime.UtcNow) }; documents[id] = updated; AddAudit(user, request.Approved ? "Approved" : "Returned", "CompanyDocument", id, request.Comments); return updated;
    }
    public (string Path, string FileName) GetDocumentFile(Guid id) { if (!documents.TryGetValue(id, out var item) || item.Status is not (RecordStatus.Approved or RecordStatus.Expired)) throw new KeyNotFoundException("Approved document not found."); return (item.StoredPath, item.FileName); }

    public IReadOnlyCollection<Accreditation> GetAccreditations() => accreditations.Values.OrderBy(a => a.ValidTo).ToArray();
    public async Task<Accreditation> AddAccreditationAsync(AccreditationRequest request, IFormFile evidence, string user, CancellationToken cancellationToken)
    {
        await scanner.EnsureSafeAsync(evidence, cancellationToken); var id = Guid.NewGuid(); var path = await SaveAsync(evidence, id, cancellationToken); var item = new Accreditation(id, request.IssuingBody, request.Scope, request.Site, request.ValidFrom, request.ValidTo, request.ResponsibleStaff, RecordStatus.Uploaded, Path.GetFileName(evidence.FileName), path); accreditations[id] = item; AddAudit(user, "Uploaded", "Accreditation", id, item.IssuingBody); return item;
    }
    public IReadOnlyCollection<Programme> GetProgrammes() => programmes.Values.OrderBy(p => p.Name).ToArray();
    public Programme AddProgramme(ProgrammeRequest request, string user) { var item = new Programme(Guid.NewGuid(), request.Name, request.Code, request.Requirements, ProgrammeStatus.Draft, []); programmes[item.Id] = item; AddAudit(user, "Created", "Programme", item.Id, item.Code); return item; }
    public Programme ApproveProgramme(Guid id, string user) { if (!programmes.TryGetValue(id, out var item)) throw new KeyNotFoundException("Programme not found."); var approved = item with { Status = ProgrammeStatus.Approved }; programmes[id] = approved; AddAudit(user, "Approved", "Programme", id, item.Code); return approved; }
    public IReadOnlyCollection<Practitioner> GetPractitioners() => practitioners.Values.OrderBy(p => p.FullName).ToArray();
    public async Task<Practitioner> AddPractitionerAsync(PractitionerRequest request, IFormFile cv, string user, CancellationToken cancellationToken) { await scanner.EnsureSafeAsync(cv, cancellationToken); var id = Guid.NewGuid(); var path = await SaveAsync(cv, id, cancellationToken); var item = new Practitioner(id, request.FullName, request.Email, request.Qualifications, request.Skills, Path.GetFileName(cv.FileName), path, false); practitioners[id] = item; AddAudit(user, "Created", "Practitioner", id, item.Email); return item; }
    public IReadOnlyCollection<LearningMaterial> GetMaterials() => materials.Values.Where(m => m.Status == RecordStatus.Approved).OrderByDescending(m => m.CreatedAt).ToArray();
    public async Task<LearningMaterial> AddMaterialAsync(MaterialRequest request, IFormFile file, string user, CancellationToken cancellationToken) { await scanner.EnsureSafeAsync(file, cancellationToken); var id = Guid.NewGuid(); var path = await SaveAsync(file, id, cancellationToken); var item = new LearningMaterial(id, request.Title, request.Category, request.Description, Path.GetFileName(file.FileName), path, RecordStatus.Uploaded, request.Version, DateTimeOffset.UtcNow); materials[id] = item; AddAudit(user, "Uploaded", "LearningMaterial", id, item.Title); return item; }
    public LearningMaterial ReviewMaterial(Guid id, ReviewRequest request, string user) { if (!materials.TryGetValue(id, out var item)) throw new KeyNotFoundException("Material not found."); var updated = item with { Status = request.Approved ? RecordStatus.Approved : RecordStatus.Returned }; materials[id] = updated; AddAudit(user, request.Approved ? "Approved" : "Returned", "LearningMaterial", id, request.Comments); return updated; }
    public (string Path, string FileName) GetMaterialFile(Guid id) { if (!materials.TryGetValue(id, out var item) || item.Status != RecordStatus.Approved) throw new KeyNotFoundException("Approved material not found."); return (item.StoredPath, item.FileName); }
    public SlaDocument GenerateSla(SlaRequest request, string user)
    {
        if (!practitioners.TryGetValue(request.PractitionerId, out var practitioner) || !programmes.TryGetValue(request.ProgrammeId, out var programme)) throw new KeyNotFoundException("Practitioner or programme not found.");
        if (programme.Status != ProgrammeStatus.Approved) throw new InvalidOperationException("Only approved programmes may be used in agreements.");
        var result = new SlaDocument($"SLA-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}", DateTimeOffset.UtcNow, practitioner.FullName, programme.Name, request.StartDate, request.EndDate, request.Scope, request.Terms, "Indonsa Training"); AddAudit(user, "Generated", "Sla", Guid.NewGuid(), result.AgreementNumber); return result;
    }
    public IReadOnlyCollection<AuditEntry> GetAudit() => audit.OrderByDescending(a => a.At).ToArray();
    public IReadOnlyCollection<Alert> FindDueAlerts(DateOnly today)
    {
        var alerts = new List<Alert>();
        foreach (var document in documents.Values.Where(d => d.Status == RecordStatus.Approved && d.ExpiryDate is not null))
        {
            var days = document.ExpiryDate.Value.DayNumber - today.DayNumber;
            if (days is not (14 or 7)) continue;
            var recipient = string.Join(';', document.AuthorisedRecipients);
            var delivery = QueueNotification(document.Id, $"CompanyDocument-{days}", recipient, document.ExpiryDate.Value);
            if (delivery.Status == NotificationStatus.Queued) alerts.Add(new Alert(recipient, $"Action required - {document.Title} expires in {days} days", $"The record {document.Title} expires on {document.ExpiryDate:yyyy-MM-dd}. Obtain updated evidence and submit it for review.", document.Id, DateTimeOffset.UtcNow));
        }

        foreach (var accreditation in accreditations.Values.Where(a => a.Status == RecordStatus.Approved && a.ValidTo is not null))
        {
            if (accreditation.ValidTo.Value.DayNumber - today.DayNumber != 30) continue;
            var delivery = QueueNotification(accreditation.Id, "Accreditation-30", accreditation.ResponsibleStaff, accreditation.ValidTo.Value);
            if (delivery.Status == NotificationStatus.Queued) alerts.Add(new Alert(accreditation.ResponsibleStaff, $"Accreditation expiry - {accreditation.IssuingBody}", $"The accreditation scope {accreditation.Scope} expires on {accreditation.ValidTo:yyyy-MM-dd}.", accreditation.Id, DateTimeOffset.UtcNow));
        }

        return alerts;
    }

    public IReadOnlyCollection<AccreditationApplication> GetAccreditationApplications() => applications.Values.OrderByDescending(a => a.CreatedAt).ToArray();

    public AccreditationApplication AddAccreditationApplication(AccreditationApplicationRequest request, string user)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.ResponsiblePerson)) throw new ArgumentException("Title and responsible person are required.");
        var item = new AccreditationApplication(Guid.NewGuid(), request.Title, request.QualificationOrSkillsProgramme, request.Site, request.ProgrammeId, AccreditationApplicationStatus.Identified, request.TargetSubmissionDate, request.ResponsiblePerson, request.RequiredSiteChecks, DateTimeOffset.UtcNow);
        applications[item.Id] = item;
        AddAudit(user, "Created", "AccreditationApplication", item.Id, item.Title);
        return item;
    }

    public AccreditationApplication UpdateAccreditationApplication(Guid id, AccreditationApplicationStatus status, string user)
    {
        if (!applications.TryGetValue(id, out var item)) throw new KeyNotFoundException("Accreditation application not found.");
        var updated = item with { Status = status };
        applications[id] = updated;
        AddAudit(user, "StatusChanged", "AccreditationApplication", id, status.ToString());
        return updated;
    }

    public IReadOnlyCollection<EvidenceRequirement> GetEvidenceRequirements(Guid applicationId) => evidenceRequirements.Values.Where(e => e.ApplicationId == applicationId).OrderBy(e => e.Folder).ThenBy(e => e.Requirement).ToArray();

    public EvidenceRequirement AddEvidenceRequirement(Guid applicationId, EvidenceRequirementRequest request, string user)
    {
        if (!applications.ContainsKey(applicationId)) throw new KeyNotFoundException("Accreditation application not found.");
        var item = new EvidenceRequirement(Guid.NewGuid(), applicationId, request.Folder, request.Requirement, request.Mandatory, EvidenceRequirementStatus.NotStarted, null, null, null, null, 1);
        evidenceRequirements[item.Id] = item;
        AddAudit(user, "RequirementAdded", "EvidenceRequirement", item.Id, item.Requirement);
        return item;
    }

    public EvidenceRequirement ReviewEvidenceRequirement(Guid id, EvidenceReviewRequest request, string user)
    {
        if (!evidenceRequirements.TryGetValue(id, out var item)) throw new KeyNotFoundException("Evidence requirement not found.");
        if (request.Status == EvidenceRequirementStatus.Satisfied && request.DocumentId is null) throw new ArgumentException("Satisfied evidence must reference a company document.");
        var updated = item with { Status = request.Status, DocumentId = request.DocumentId, Reviewer = user, ReviewedAt = DateTimeOffset.UtcNow, ReviewerReason = request.Reason, Version = item.Version + 1 };
        evidenceRequirements[id] = updated;
        AddAudit(user, "EvidenceReviewed", "EvidenceRequirement", id, request.Reason);
        return updated;
    }

    public IReadOnlyCollection<NotificationDelivery> GetNotificationDeliveries() => notificationDeliveries.Values.OrderByDescending(n => n.DueDate).ToArray();

    public void MarkNotification(NotificationDelivery notification, NotificationStatus status, string? error = null)
    {
        notificationDeliveries.AddOrUpdate(notification.Id, notification with { Status = status, SentAt = DateTimeOffset.UtcNow, Error = error }, (_, current) => current with { Status = status, SentAt = DateTimeOffset.UtcNow, Error = error });
    }

    private NotificationDelivery QueueNotification(Guid entityId, string category, string recipient, DateOnly dueDate)
    {
        var existing = notificationDeliveries.Values.FirstOrDefault(n => n.EntityId == entityId && n.Category == category && n.DueDate == dueDate);
        if (existing is not null) return existing;
        var item = new NotificationDelivery(Guid.NewGuid(), entityId, category, recipient, dueDate, NotificationStatus.Queued, null, null);
        notificationDeliveries[item.Id] = item;
        return item;
    }

    private async Task<string> SaveAsync(IFormFile file, Guid id, CancellationToken cancellationToken) { Directory.CreateDirectory(storageRoot); var path = Path.Combine(storageRoot, id + Path.GetExtension(file.FileName).ToLowerInvariant()); await using var output = File.Create(path); await file.CopyToAsync(output, cancellationToken); return path; }
    private static void ValidateDocument(CompanyDocumentRequest r) { if (string.IsNullOrWhiteSpace(r.Title) || string.IsNullOrWhiteSpace(r.Category) || string.IsNullOrWhiteSpace(r.Owner)) throw new ArgumentException("Title, category, and owner are required."); if (r.ExpiryDate is null && string.IsNullOrWhiteSpace(r.CompanyOrSite)) { } }
    private void AddAudit(string user, string action, string type, Guid id, string? details) => audit.Add(new AuditEntry(Guid.NewGuid(), DateTimeOffset.UtcNow, user, action, type, id, details));
}

namespace WebApplication2.Portal;

public enum RecordStatus { Uploaded, Reviewed, Approved, Expired, Superseded, Returned }
public enum Sensitivity { Internal, Confidential, Restricted }
public enum ProgrammeStatus { Draft, Approved, Retired }
public enum AccreditationApplicationStatus { Identified, FeasibilityReview, EvidencePreparation, InternalReview, Submitted, QueriesOrSiteVisit, DecisionReceived, Withdrawn, Expired, Suspended }
public enum EvidenceRequirementStatus { NotStarted, InProgress, Satisfied, NotApplicable, Returned }
public enum NotificationStatus { Queued, Accepted, Delivered, Bounced, Failed }

public static class PortalRoles
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string ComplianceAdministrator = "ComplianceAdministrator";
    public const string AccreditationQualityManager = "AccreditationQualityManager";
    public const string TrainingCoordinator = "TrainingCoordinator";
    public const string TenderOfficer = "TenderOfficer";
    public const string AuthorisedSignatory = "AuthorisedSignatory";
    public const string FacilitatorAssessor = "FacilitatorAssessor";
    public const string AuditorReviewer = "AuditorReviewer";
}

public sealed record AuditEntry(Guid Id, DateTimeOffset At, string User, string Action, string EntityType, Guid EntityId, string? Details);
public sealed record CompanyDocument(Guid Id, string Title, string Category, string Issuer, string ReferenceNumber, string? CompanyOrSite, DateOnly DocumentDate, DateOnly? UploadDate, DateOnly? ExpiryDate, DateOnly? CertificationDate, DateOnly? ReviewDate, string Owner, string[] AuthorisedRecipients, Sensitivity Sensitivity, int Version, RecordStatus Status, string FileName, string StoredPath, Guid? PreviousVersionId, string? ReviewComments);
public sealed record Accreditation(Guid Id, string IssuingBody, string Scope, string Site, DateOnly? ValidFrom, DateOnly? ValidTo, string ResponsibleStaff, RecordStatus Status, string EvidenceFileName, string EvidencePath);
public sealed record Programme(Guid Id, string Name, string Code, string Requirements, ProgrammeStatus Status, Guid[] PractitionerIds);
public sealed record Practitioner(Guid Id, string FullName, string Email, string[] Qualifications, string[] Skills, string CvFileName, string CvPath, bool Invited);
public sealed record LearningMaterial(Guid Id, string Title, string Category, string Description, string FileName, string StoredPath, RecordStatus Status, string Version, DateTimeOffset CreatedAt);
public sealed record Dashboard(DateTimeOffset GeneratedAt, IReadOnlyList<CompanyDocument> ExpiringDocuments, IReadOnlyList<CompanyDocument> ExpiredDocuments, IReadOnlyList<Accreditation> ExpiringAccreditations, IReadOnlyList<AuditEntry> PendingReviews);
public sealed record CompanyDocumentRequest(string Title, string Category, string Issuer, string ReferenceNumber, string? CompanyOrSite, DateOnly DocumentDate, DateOnly? ExpiryDate, DateOnly? CertificationDate, DateOnly? ReviewDate, string Owner, string[] AuthorisedRecipients, Sensitivity Sensitivity);
public sealed record AccreditationRequest(string IssuingBody, string Scope, string Site, DateOnly? ValidFrom, DateOnly? ValidTo, string ResponsibleStaff);
public sealed record ProgrammeRequest(string Name, string Code, string Requirements);
public sealed record PractitionerRequest(string FullName, string Email, string[] Qualifications, string[] Skills);
public sealed record MaterialRequest(string Title, string Category, string Description, string Version);
public sealed record ReviewRequest(bool Approved, string? Comments);
public sealed record SlaRequest(Guid PractitionerId, Guid ProgrammeId, DateOnly StartDate, DateOnly EndDate, string Scope, string Terms);
public sealed record SlaDocument(string AgreementNumber, DateTimeOffset GeneratedAt, string PractitionerName, string ProgrammeName, DateOnly StartDate, DateOnly EndDate, string Scope, string Terms, string CompanyName);
public sealed record Alert(string Recipient, string Subject, string Body, Guid EntityId, DateTimeOffset CreatedAt);
public sealed record AccreditationApplication(Guid Id, string Title, string QualificationOrSkillsProgramme, string Site, Guid? ProgrammeId, AccreditationApplicationStatus Status, DateOnly TargetSubmissionDate, string ResponsiblePerson, string[] RequiredSiteChecks, DateTimeOffset CreatedAt);
public sealed record EvidenceRequirement(Guid Id, Guid ApplicationId, string Folder, string Requirement, bool Mandatory, EvidenceRequirementStatus Status, Guid? DocumentId, string? Reviewer, DateTimeOffset? ReviewedAt, string? ReviewerReason, int Version);
public sealed record AccreditationApplicationRequest(string Title, string QualificationOrSkillsProgramme, string Site, Guid? ProgrammeId, DateOnly TargetSubmissionDate, string ResponsiblePerson, string[] RequiredSiteChecks);
public sealed record EvidenceRequirementRequest(string Folder, string Requirement, bool Mandatory);
public sealed record EvidenceReviewRequest(EvidenceRequirementStatus Status, Guid? DocumentId, string? Reason);
public sealed record NotificationDelivery(Guid Id, Guid EntityId, string Category, string Recipient, DateOnly DueDate, NotificationStatus Status, DateTimeOffset? SentAt, string? Error);

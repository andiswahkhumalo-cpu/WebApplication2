# Indonsa Internal Staff Portal API

This .NET 10 API implements the first secure internal-portal slice from the Indonsa Training System Guidelines:

- Company document register with metadata, sensitivity, explicit non-expiring dates, review/approval, version-ready records, controlled downloads, audit entries, and 14/7-day expiry alerts.
- Accreditation register with evidence and 30-day alerts.
- Programme and practitioner registers with qualifications, skills, CV evidence, and approved-programme checks.
- Learning material library with review/approval and controlled downloads.
- Role-aware service-level agreement generation from approved programme and practitioner records.
- Dashboard and audit endpoints.

## Security and production integration

All portal endpoints require an authenticated identity and role policies matching the guideline roles. The application intentionally does not invent an identity provider: connect ASP.NET Core authentication to the organization's Entra ID/OIDC/JWT provider and map its groups to these role values:

`SystemAdministrator`, `ComplianceAdministrator`, `AccreditationQualityManager`, `TrainingCoordinator`, `TenderOfficer`, `AuthorisedSignatory`, `FacilitatorAssessor`, and `AuditorReviewer`.

Uploaded files are limited to 25 MB and a small approved extension list. `BasicFileSafetyScanner` is only a development-safe placeholder; wire it to an enterprise malware scanner before production use. Replace the in-memory `PortalService` with a database-backed implementation and replace `LoggingAlertSender` with the approved email provider. Local files are stored under `App_Data/portal-files` and should be moved to encrypted managed storage with access logging.

## API areas

- `GET /api/portal/dashboard`
- `GET/POST /api/portal/documents`, `POST /api/portal/documents/{id}/review`, `GET /api/portal/documents/{id}/download`
- `GET/POST /api/portal/accreditations`
- `GET/POST /api/portal/programmes`, `POST /api/portal/programmes/{id}/approve`
- `GET/POST /api/portal/practitioners`
- `GET/POST /api/portal/materials`, `POST /api/portal/materials/{id}/review`, `GET /api/portal/materials/{id}/download`
- `POST /api/portal/service-level-agreements`
- `GET /api/portal/audit`

The reminder worker evaluates due dates daily using UTC dates. The starter project still exposes the generated weather endpoint; it is unrelated to the portal and can be removed when the client UI is introduced.

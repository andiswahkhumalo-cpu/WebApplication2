using WebApplication2.Portal;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IPortalService, PortalService>();
builder.Services.AddSingleton<IFileSafetyScanner, BasicFileSafetyScanner>();
builder.Services.AddSingleton<IAlertSender, LoggingAlertSender>();
builder.Services.AddHostedService<ReminderWorker>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("PortalReader", p => p.RequireAuthenticatedUser())
    .AddPolicy("DocumentManager", p => p.RequireRole(PortalRoles.ComplianceAdministrator, PortalRoles.SystemAdministrator))
    .AddPolicy("DocumentReviewer", p => p.RequireRole(PortalRoles.ComplianceAdministrator, PortalRoles.AuditorReviewer, PortalRoles.SystemAdministrator))
    .AddPolicy("AccreditationManager", p => p.RequireRole(PortalRoles.AccreditationQualityManager, PortalRoles.SystemAdministrator))
    .AddPolicy("ProgrammeManager", p => p.RequireRole(PortalRoles.AccreditationQualityManager, PortalRoles.TrainingCoordinator, PortalRoles.SystemAdministrator))
    .AddPolicy("PractitionerManager", p => p.RequireRole(PortalRoles.TrainingCoordinator, PortalRoles.SystemAdministrator))
    .AddPolicy("MaterialManager", p => p.RequireRole(PortalRoles.AccreditationQualityManager, PortalRoles.SystemAdministrator))
    .AddPolicy("AgreementManager", p => p.RequireRole(PortalRoles.AuthorisedSignatory, PortalRoles.TenderOfficer, PortalRoles.SystemAdministrator))
    .AddPolicy("Auditor", p => p.RequireRole(PortalRoles.AuditorReviewer, PortalRoles.SystemAdministrator));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseDefaultFiles();
app.UseStaticFiles();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseAuthorization();

app.MapControllers();

app.Run();

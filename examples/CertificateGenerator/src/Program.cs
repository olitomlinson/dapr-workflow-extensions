using Dapr.Client;
using Dapr.Workflow;
using DaprWorkflowExtensions.WorkflowOutput;
using Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddDaprWorkflow(options =>
    {
        options.RegisterWorkflow<GenerateCertificateWorkflow>();
        options.RegisterActivity<SendRedeemCodeToUser>();
        options.RegisterActivity<GenerateCertificateBase64>();
    });

builder.Services.AddControllers();

var app = builder.Build();

app.MapPost("/GenerateCertificate/start", async ( DaprClient daprClient, DaprWorkflowClient workflowClient, Input request) => {
    await daprClient.WaitForSidecarAsync();

    return new
    {
        WorkflowInstanceId = await workflowClient.ScheduleNewWorkflowAsync(
            name: nameof(GenerateCertificateWorkflow),
            input: request)
    };   
});

app.MapPost("/GenerateCertificate/{id}/Redeem", async ( DaprClient daprClient, DaprWorkflowClient workflowClient, string id, ReedeemCode redeemCode) => {
    await daprClient.WaitForSidecarAsync();

    await workflowClient.RaiseEventAsync(id,"RedeemCodeAttempt" ,redeemCode.code);

    return Results.Ok(); 
});

app.MapGet("/GenerateCertificate/{id}", async ( DaprClient daprClient, DaprWorkflowClient workflowClient, string id, bool? justLogs) => {
    await daprClient.WaitForSidecarAsync();

    var state = await workflowClient.GetWorkflowStateAsync(id);
    var (certificate, status, logs) = state.Get<CertificateFile, CertificateStatus>();

    if (state.RuntimeStatus == WorkflowRuntimeStatus.Failed)
        return Results.Json(new { error = "There was an error during the certificate redeem process. Seek support" }, statusCode: 500);

    if (justLogs.GetValueOrDefault(false))
        return Results.Json(new { Logs = logs?.Select(x => $"{x.Timestamp} {x.Message}") }); 

    if (status == CertificateStatus.CertificateGenerated) {
        return Results.File(Convert.FromBase64String(certificate.FileData), "application/pdf", certificate.FileName);
    }
    else {
        //handle other statuses...
    }

    return Results.Ok();
});


app.Run();

public record ReedeemCode(string code);
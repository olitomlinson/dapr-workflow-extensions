This library provides an opinionated way of authoring the various outputs associated with a Dapr Workflow. In doing so, this hides the authoring boilerplate of the workflow and simplifies the access of workflow output to consumers.

# Quick Example

## Authoring a Dapr Workflow with this library...

The opinionated model requires you to define a `status` type which represents the various states that are important to track in you business process, in addition to the normal Workflow `input` and `output`. In this example, the `status` type has been chosen as an `enum`, but could be any serializable you chose.

```c#
    public enum CertificateStatus
    {
        Started, SendingRedeemCode, WaitingForUserToRedeem, RedeemCodeInvalid, Redeemed, CertificateGenerated
    }
    // The 'Input' to the Workflow
    public record Input(string UserFriendlyName, string UserId);
    // The 'Output' to the Workflow
    public record CertificateFile(string FileName, string FileData);

```

In addition to setting the `status` and `output`, the library provides an additional capability to set logs too.

Use the `WorkflowContext` extension method `BuildOutputHelper<TOutputValue,TStatusValue>` to create the convenience functions for setting `status`, `output`, and `logs`. Examples of all three convenience functions are in the Workflow code below

```c#
    public class GenerateCertificateWorkflow : Workflow<Input, WorkflowOutput<CertificateFile, CertificateStatus>>
    {
        public override async Task<WorkflowOutput<CertificateFile,CertificateStatus>> RunAsync(WorkflowContext context, Input payload)
        {
            var (log, status, output) = context.BuildOutputHelper<CertificateFile,CertificateStatus>(CertificateStatus.Started, null);
            
            status(CertificateStatus.SendingRedeemCode);
            log($"sending unique redeem code to '{payload.UserFriendlyName}'");

            var code = await context.CallActivityAsync<string>("SendRedeemCodeToUser", payload);
            status(CertificateStatus.WaitingForUserToRedeem);
            log($"Waiting for user to supply code {code}...");

            var codeAttempt = await context.WaitForExternalEventAsync<string>("RedeemCodeAttempt");
            if (code != codeAttempt) {
                status(CertificateStatus.RedeemCodeInvalid);
                log($"User supplied incorrect code {codeAttempt}");
                return output(null);
            }

            status(CertificateStatus.Redeemed);
            log($"Code redeemed successfully'");

            var startTime = context.CurrentUtcDateTime;
            var certificate_base64 = await context.CallActivityAsync<string>("GenerateCertificateBase64", payload.UserId);
            status(CertificateStatus.CertificateGenerated);
            log($"Certificate creation took {context.CurrentUtcDateTime.Subtract(startTime).TotalSeconds} seconds");
            return output(new CertificateFile($"{payload.UserFriendlyName} - Certificate.pdf", certificate_base64));
        }
    }

```

**Important:** The `output()` function also returns the `WorkflowOutput<TStatusValue, TOutputValue>` object. You **must** exit the Workflow using this returned object.

**Note:** When using the `status()` function, a convenient log message will be automatically appened to the logs to represent the status transistion.

 ```c#
    {
        "logs": [
            "03/28/2024 22:50:12 Initial status set to 'Started'",
            "03/28/2024 22:50:12 Status changed from 'Started' to 'SendingRedeemCode'",
            "03/28/2024 22:50:12 sending unique redeem code to '123'",
            "03/28/2024 22:50:12 Status changed from 'SendingRedeemCode' to 'WaitingForUserToRedeem'",
            "03/28/2024 22:50:12 Waiting for user to supply code 9477...",
            "03/28/2024 22:52:49 Status changed from 'WaitingForUserToRedeem' to 'Redeemed'",
            "03/28/2024 22:52:49 Code redeemed successfully'",
            "03/28/2024 22:52:49 Status changed from 'Redeemed' to 'CertificateGenerated'",
            "03/28/2024 22:52:49 Certificate creation took 0.0126271 seconds"
        ]
    }
  ```

## Example continued : Consuming the state of the workflow...

Use the `WorkflowState` extension method `Get<TOutputValue,TStatusValue>` to deconstruct the `output`, `status`, and `logs` values. 

The library ensures that the `output`, `status`, and `logs` are consistent and available across both `running`,`terminated`,`paused`, and `completed` workflow runtime statuses.

Given this opinionated model, your consuming code is now significantly simplified :

```c#
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
```


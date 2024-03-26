using Dapr.Workflow;
using DaprWorkflowExtensions.WorkflowOutput;
using System.Text.Json.Serialization;

namespace Workflows
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    // The user-defined 'Status' of the business process
    public enum CertificateStatus
    {
        Started, SendingRedeemCode, WaitingForUserToRedeem, RedeemCodeInvalid, Redeemed, CertificateGenerated
    }
    // The 'Input' to the Workflow
    public record Input(string UserFriendlyName, string UserId);
    // The 'Output' to the Workflow
    public record CertificateFile(string FileName, string FileData);

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
}

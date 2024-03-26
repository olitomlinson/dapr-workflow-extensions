using Dapr.Workflow;

namespace Workflows
{
    public class SendRedeemCodeToUser : WorkflowActivity<Input, string>
    {
        readonly ILogger logger;

        public SendRedeemCodeToUser(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<SendRedeemCodeToUser>();
        }

        public override async Task<string> RunAsync(WorkflowActivityContext context, Input input)
        {
            // Simulate calling a service to send the code over email/sms to the end-user

            Random number = new Random();
            var code = number.Next(0, 9999).ToString();
            code = code.PadLeft(4,'0');
            return code;
        }
    }
}
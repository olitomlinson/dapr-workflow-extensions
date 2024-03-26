using System.Text.Json.Serialization;
using Dapr.Workflow;

namespace DaprWorkflowExtensions.WorkflowOutput
{
    public static class DaprWorkflowContextExtensions
    {
        public static WorkflowOutputHelper<TOutputValue, TStatusValue> BuildOutputHelper<TOutputValue,TStatusValue>(this WorkflowContext context, TStatusValue initialStatus, string? logPrefix)
        {
            return new WorkflowOutputHelper<TOutputValue, TStatusValue>(context, initialStatus, logPrefix);
        }
    }

    public static class DaprWorkflowStateExtensions
    {
        public static WorkflowOutput<TOutputValue, TStatusValue> Get<TOutputValue, TStatusValue>(this WorkflowState state)
        {
            if (state.IsWorkflowCompleted)
                return state.ReadOutputAs<WorkflowOutput<TOutputValue,TStatusValue>>();
            else
            {
                return state.ReadCustomStatusAs<WorkflowOutput<TOutputValue,TStatusValue>>() ?? 
                    new WorkflowOutput<TOutputValue, TStatusValue>();
            }
        } 
    }

    public record Log(DateTime Timestamp, string Message);

    public class WorkflowOutput<TOutputValue,TStatusValue>
    {
        [JsonInclude]
        public TOutputValue? Output {get; internal set;}
        [JsonInclude]
        public TStatusValue? Status { get; internal set;}
        [JsonInclude]
        public List<Log>? Logs {get; internal set;}

        public void Deconstruct(out TOutputValue? output, out TStatusValue? status, out List<Log>? logs){
            output = Output;
            status = Status;
            logs = Logs;
        }
    }

    public class WorkflowOutputHelper<TOutputValue, TStatusValue> : WorkflowOutput<TOutputValue, TStatusValue>
    {
        private Action<string> LogFn {get;set;}
        private Action<TStatusValue> SetStatusFn {get;set;}
        private Func<TOutputValue, WorkflowOutput<TOutputValue,TStatusValue>> SetOutputFn {get;set;}

        public WorkflowOutputHelper(WorkflowContext context, TStatusValue initialStatus, string logPrefix)
        {
            Logs = new List<Log>();

            LogFn = (string message) => { 
                var timestamp = context.CurrentUtcDateTime;
                message = !string.IsNullOrEmpty(logPrefix) ? $"{logPrefix} {message}" : $"{message}";
                Logs.Add(new(context.CurrentUtcDateTime, message));
                context.SetCustomStatus(this);
            };

            SetStatusFn = (TStatusValue newStatus) => {
                var oldStatus = Status;
                Status = newStatus;
                LogFn($"Status changed from '{oldStatus}' to '{Status}'");
            };

            SetOutputFn = (TOutputValue output) => {
                context.SetCustomStatus(null);
                Output = output;
                return this;
            };

            Status = initialStatus;
            LogFn($"Initial status set to '{Status}'");
        }
        
        public void Deconstruct(out Action<string> log, out Action<TStatusValue> status, out Func<TOutputValue,WorkflowOutput<TOutputValue,TStatusValue>> output)
        {
            log = LogFn;
            status = SetStatusFn;
            output = SetOutputFn;
        }
    }
}
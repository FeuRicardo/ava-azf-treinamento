using HelloMe.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HelloMe.App
{
    public class Hello
    {
        private readonly IHelloCustomSingleton customSingleton;
        private readonly IHelloCustomSingleton customSingleton2;
        private readonly IHelloCustomScoped customScoped;
        private readonly IHelloCustomScoped customScoped2;
        private readonly IConfig config;
        private readonly IHelloCustomTransient customTransient;
        private readonly IHelloCustomTransient customTransient2;

        public Hello(IHelloCustomSingleton customSingleton, IHelloCustomSingleton customSingleton2,
                     IHelloCustomTransient customTransient, IHelloCustomTransient customTransient2,
                     IHelloCustomScoped customScoped, IHelloCustomScoped customScoped2,
                     IConfig config
                     )
        {
            this.customSingleton = customSingleton;
            this.customSingleton2 = customSingleton2;
            this.customScoped = customScoped;
            this.customScoped2 = customScoped2;
            this.config = config;
            this.customTransient = customTransient;
            this.customTransient2 = customTransient2;
        }

        #region HANDSON
        [FunctionName("hello")]
        public static async Task<IActionResult> HelloMe(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "hello")] HttpRequest req,
            ILogger log)
        {
            string name = req.Query["name"];
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name ??= data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "Hello anonymous. What's your name?"
                : $@"Hello, {name}. It's a test \o/";

            return new OkObjectResult(responseMessage);
        }
        #endregion

        #region HANDSON-HTTP_INJECTION
        private IActionResult BuildResponse(HttpRequest req, IHelloCustom hello1, IHelloCustom hello2)
        {
            var response = new StringBuilder();
            var name = req.Query["name"];

            response.Append($"Instance 1 => {hello1.GetMessage(name)}");
            response.Append(Environment.NewLine);
            response.Append($"Instance 2 => {hello2.GetMessage(name)}");

            return new OkObjectResult(response.ToString());
        }

        [FunctionName("HelloSingleton")]
        public IActionResult HelloSingleton(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "hello/singleton")] HttpRequest req,
            ILogger log)
        {
            return BuildResponse(req, customSingleton, customSingleton2);
        }

        [FunctionName("HelloScoped")]
        public IActionResult HelloScoped(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "hello/scoped")] HttpRequest req,
            ILogger log)
        {
            return BuildResponse(req, customScoped, customScoped2);
        }

        [FunctionName("HelloTransient")]
        public IActionResult HelloTransient(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "hello/transient")] HttpRequest req,
            ILogger log)
        {
            return BuildResponse(req, customTransient, customTransient2);
        }
        #endregion

        #region HANDSON-EVENT_HUB
        [FunctionName("helloEH")]
        public static async Task<IActionResult> ForwardMessageEH([HttpTrigger(AuthorizationLevel.Function, "post", Route = "forwardHello")] HttpRequest req,
            [EventHub("%ehName%", Connection = "EHConnectionString")] IAsyncCollector<string> outputEvents,
            ILogger log)
        {
            string name = req.Query["name"];
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name ??= data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "Hello anonymous. What's your name?"
                : $@"Hello, {name}. It's a test \o/";

            await outputEvents.AddAsync(responseMessage);

            return new OkObjectResult($"Message '{responseMessage}' has sent successfuly");
        }
        #endregion

        #region HANDSON-DURABLE
        [FunctionName("helloDurable")]
        public static async Task<IActionResult> HelloDurable([HttpTrigger(AuthorizationLevel.Function, "post", Route = "helloDurable")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string name = req.Query["name"];
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name ??= data?.name;

            ///Starting new orchatrator
            var instanceId = await starter.StartNewAsync("Orchestrator");


            HttpManagementPayload httpManagement = starter.CreateHttpManagementPayload(instanceId);
            log.LogWarning(JsonConvert.SerializeObject(httpManagement));

            ///Waiting for the orchestrator to complete
            //while (
            //    ((await starter.GetStatusAsync(instanceId)).RuntimeStatus == OrchestrationRuntimeStatus.Pending) ||
            //    ((await starter.GetStatusAsync(instanceId)).RuntimeStatus == OrchestrationRuntimeStatus.Running)
            //    )
            //{
            //    await Task.Delay(500);
            //}

            return new OkObjectResult(httpManagement);
        }

        [FunctionName("Orchestrator")]
        public async Task OrchestratorSTR(
                [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var name = context.GetInput<string>();
            try
            {
                var message = await context.CallActivityAsync<string>("DurableFormatMessage", name);
                var result = await context.CallActivityAsync<string>("DurableSendMessage", message);
                log.LogInformation($"Durable has complted - {result}");
            }
            catch (Exception e)
            {
                log.LogError($"Error orchestrating the task to send message => {e.Message}");
            }
        }

        [FunctionName("DurableFormatMessage")]
        public async Task<string> DurableFormatMessage([ActivityTrigger] string name, ILogger log)
        {
            await Task.Delay(30000);
            string responseMessage = $@"Hello, {name}. It's a durable test ";
            return responseMessage;
        }

        [FunctionName("DurableSendMessage")]
        public async Task<string> DurableSendMessage([ActivityTrigger] string message, ILogger log)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes(message);
                var connStr = config.EHConnectionString;
                var builder = new EventHubsConnectionStringBuilder(connStr)
                {
                    TransportType = TransportType.Amqp,
                    OperationTimeout = TimeSpan.FromSeconds(120)
                };
                var ehClient = EventHubClient.CreateFromConnectionString(builder.ToString());
                await ehClient.SendAsync(new EventData(data));
                await ehClient.CloseAsync();

                return "Success";
            }
            catch (Exception e)
            {
                log.LogCritical(e.Message);
            }

            return "Failed";
        }
        #endregion

        #region DURABLE-WAIT_FOR_EVENT
        [FunctionName("BudgetApproval")]
        public static async Task Run([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            bool approved = await context.WaitForExternalEvent<bool>("Approval");
            if (approved)
            {
                log.LogWarning("approval granted - do the approved action");
            }
            else
            {
                log.LogWarning("approval denied - send a notification");
            }
        }

        [FunctionName("DurableApproveRefuse")]
        public static async Task DurableEvent(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "DurableApproveRefuse/{instance}/{status}")] HttpRequest req, string instance, bool status,            
            [DurableClient] IDurableOrchestrationClient client)
        {
            await client.RaiseEventAsync(instance, "Approval", status);
        }

        [FunctionName("DurableRequestApproval")]
        public static async Task<IActionResult> DurableWaitApproval(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "DurableRequestApproval")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient client, ILogger log)
        {
            var instance = await client.StartNewAsync("BudgetApproval");
            string message = $"ID of your request => {instance}";
            log.LogWarning(message);

            return new OkObjectResult(message);
        }
        #endregion
    }
}

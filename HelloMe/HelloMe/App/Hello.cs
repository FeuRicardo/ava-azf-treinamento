using HelloMe.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
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
        private readonly IHelloCustomTransient customTransient;
        private readonly IHelloCustomTransient customTransient2;

        public Hello(IHelloCustomSingleton customSingleton, IHelloCustomSingleton customSingleton2,
                     IHelloCustomTransient customTransient, IHelloCustomTransient customTransient2,
                     IHelloCustomScoped customScoped, IHelloCustomScoped customScoped2
                     )
        {
            this.customSingleton = customSingleton;
            this.customSingleton2 = customSingleton2;
            this.customScoped = customScoped;
            this.customScoped2 = customScoped2;
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
    }
}

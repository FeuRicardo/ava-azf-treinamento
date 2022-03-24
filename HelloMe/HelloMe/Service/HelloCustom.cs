using HelloMe.Interface;
using System;

namespace HelloMe.Service
{
    public abstract class HelloCustom : IHelloCustom
    {
        private string MyGuid = Guid.NewGuid().ToString();
        public string GetMessage(string name)
        {
            var message = string.IsNullOrEmpty(name) ? $"The instance '{MyGuid} asks your name" : $"The instance '{MyGuid} says hello to {name}";

            return message;
        }
    }
}

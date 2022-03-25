using HelloMe.Interface;

namespace HelloMe.App
{
    public class Config : IConfig
    {
        public string EHConnectionString { get; set; }
    }
}

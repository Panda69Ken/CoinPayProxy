using CoinPayProxy.Infrastructure.Enums;
using CoinPayProxy.Rpc.Core.Config;

namespace CoinPayProxy.Rpc.Core.Service
{
    public interface IConfigService
    {
        Dictionary<AgreementTypeEnum, AgreementConfig> AgreementConfig { get; }
        List<string> NodeList { get; }
        List<JobCron> JobCrons { get; }
    }

    public class ConfigService(IConfiguration configuration) : IConfigService
    {
        private readonly IConfiguration _configuration = configuration;

        public string GetSetting(string name)
        {
            var value = _configuration.GetSection(name).Value;
            if (value == null)
            {
                return string.Empty;
            }
            return value;
        }
        public T GetSettingT<T>(string name)
        {
            var value = _configuration.GetSection(name).Get<T>();
            if (value == null)
            {
                return default;
            }
            return value;
        }

        public Dictionary<AgreementTypeEnum, AgreementConfig> AgreementConfig =>
            GetSettingT<Dictionary<AgreementTypeEnum, AgreementConfig>>("AgreementConfig");

        public List<string> NodeList => GetSettingT<List<string>>("NodeList");

        public List<JobCron> JobCrons => GetSettingT<List<JobCron>>("JobCronConfig");
    }
}

using Microsoft.Extensions.Configuration;

namespace Tlv.Search
{
    public class RedisConfig
    {
        public string? connectionString { get; set; }
        public int minThreads { get; set; }

        public static RedisConfig? Load(IConfiguration config, bool? isLocal)
        {
            if (!isLocal.HasValue)
                return null;

            var section = config.GetConnectionString("Redis");

            RedisConfig? result = null;

            //if (isLocal.Value)
            //    result = config.GetSection("RedisConfig").Get<RedisConfig>();
            //else
            //{
            var minThreads = config["RedisConfig:minThreads"];
            minThreads ??= "500";

            result = new RedisConfig()
            {
                connectionString = config["RedisConfig:connectionString"],
                minThreads = int.Parse(minThreads)
            };
            //}

            // Be sure all properties were loaded
            bool bRes = result.GetType().GetProperties()
                .All(p => p.GetValue(result) != null);

            return bRes ? result : null;
        }
    }
}

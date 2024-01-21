using BenchmarkDotNet.Configs;
using Iced.Intel;
using StackExchange.Redis;
using System;
using Microsoft.Extensions.Configuration;

namespace Scrapper
{
    public class Cache
    {
        private ConnectionMultiplexer _multiplexer { get; set; }
        private IDatabase _db { get; set; }

        public Cache(IConfiguration config, string connectionStringName)
        {
            var connectionString = config.GetConnectionString(connectionStringName);
            _multiplexer = ConnectionMultiplexer.Connect(connectionString);
            _db = _multiplexer.GetDatabase();
        }

        public void ClearAll()
        {
            var endpoints = _multiplexer.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _multiplexer.GetServer(endpoint);
                server.FlushAllDatabases();
            }
        }

        public void Merge(Dictionary<string, int> dict)
        {
            foreach (var dictKey in dict.Keys)
            {
                int count = dict[dictKey];
                _db.StringSet(dictKey, count);
            }
        }
    }
}

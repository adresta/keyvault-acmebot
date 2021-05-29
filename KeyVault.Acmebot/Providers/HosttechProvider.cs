using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using KeyVault.Acmebot.Options;

using Newtonsoft.Json;

namespace KeyVault.Acmebot.Providers
{
    public class HosttechProvider : IDnsProvider
    {
        public HosttechProvider(HosttechOptions options)
        {
            _hosttechDnsClient = new HosttechDnsClient(options.ApiToken);
        }

        private readonly HosttechDnsClient _hosttechDnsClient;
        private readonly IdnMapping _idnMapping = new IdnMapping();

        public int PropagationSeconds => 60;

        public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
        {
            var zones = await _hosttechDnsClient.ListZonesAsync();

            return zones.Select(x => new DnsZone { Id = x.Id, Name = x.Name, }).ToArray();
        }

        public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
        {
            //var recordName = $"{relativeRecordName}.{zone.Name}";

            foreach (var value in values)
            {
                await _hosttechDnsClient.CreateDnsRecordAsync(zone.Id, relativeRecordName, value);
            }
        }

        public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
        {
            var records = await _hosttechDnsClient.ListTxtRecordsAsync(zone.Id);

            var recordsToDelete = records.Where(r => r.Name == relativeRecordName);

            foreach (var record in recordsToDelete)
            {
                await _hosttechDnsClient.DeleteDnsRecordAsync(zone.Id, record.Id);
            }
        }


        private class HosttechDnsClient
        {
            public HosttechDnsClient(string apiToken)
            {
                if (apiToken is null)
                {
                    throw new ArgumentNullException(nameof(apiToken));
                }

                _httpClient = new HttpClient
                {
                    BaseAddress = new Uri("https://api.ns1.hosttech.eu/api/user/v1/")
                };

                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            }

            private readonly HttpClient _httpClient;

               public async Task<IReadOnlyList<ZoneResult>> ListZonesAsync()
            {
                var response = await _httpClient.GetAsync("zones?limit=100");

                response.EnsureSuccessStatusCode();

                var domains = await response.Content.ReadAsAsync<ApiResult<ZoneResult>>();

                return domains.Data;
            }

            public async Task<IReadOnlyList<DnsRecordResult>> ListTxtRecordsAsync(string zone)
            {
                var response = await _httpClient.GetAsync($"zones/{zone}/records?type=TXT");

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsAsync<ApiResult<DnsRecordResult>>();

                return result.Data;
            }

            public async Task CreateDnsRecordAsync(string zone, string name, string content)
            {
                var response = await _httpClient.PostAsJsonAsync($"zones/{zone}/records", new { type = "TXT", name, text = content, ttl = 600 });

                response.EnsureSuccessStatusCode();
            }

            public async Task DeleteDnsRecordAsync(string zone, string id)
            {
                var response = await _httpClient.DeleteAsync($"zones/{zone}/records/{id}");

                response.EnsureSuccessStatusCode();
            }

        }

        private class ApiResult<T>
        {
            [JsonProperty("data")]
            public T[] Data { get; set; }

        }

        private class ZoneResult
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("email")]
            public string Email { get; set; }

            [JsonProperty("nameserver")]
            public string NameServer { get; set; }
        }

        private class DnsRecordResult
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("text")]
            public string Text { get; set; }

            [JsonProperty("comment")]
            public string Comment { get; set; }

            [JsonProperty("ttl")]
            public int TTL { get; set; }
        }
    }
}

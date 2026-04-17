using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HNG_Stage_1.Services
{
    public class ExternalApiException : Exception
    {
        public ExternalApiException(string message) : base(message) { }
    }

    public class ExternalApiService : IExternalApiService
    {
        private readonly HttpClient _httpClient;

        public ExternalApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<(string TargetApi, object Data)> FetchClassificationDataAsync(string name)
        {
            throw new NotImplementedException("Not used directly. We will ftch all three concurrently.");
        }

        public async Task<(GenderizeResponse Genderize, AgifyResponse Agify, NationalizeResponse Nationalize)> FetchAllDataAsync(string name)
        {
            var genderizeTask = FetchAsync<GenderizeResponse>($"https://api.genderize.io?name={name}", "Genderize");
            var agifyTask = FetchAsync<AgifyResponse>($"https://api.agify.io?name={name}", "Agify");
            var nationalizeTask = FetchAsync<NationalizeResponse>($"https://api.nationalize.io?name={name}", "Nationalize");

            await Task.WhenAll(genderizeTask, agifyTask, nationalizeTask);

            var genderize = await genderizeTask;
            var agify = await agifyTask;
            var nationalize = await nationalizeTask;

            // Edge cases
            if (genderize.Gender == null || genderize.Count == 0)
                throw new ExternalApiException("Genderize returned an invalid response");

            if (agify.Age == null)
                throw new ExternalApiException("Agify returned an invalid response");

            if (nationalize.Country == null || nationalize.Country.Count == 0)
                throw new ExternalApiException("Nationalize returned an invalid response");

            return (genderize, agify, nationalize);
        }

        private async Task<T> FetchAsync<T>(string url, string apiName)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    throw new ExternalApiException($"{apiName} returned an invalid response");
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = JsonSerializer.Deserialize<T>(content, options);

                if (data == null)
                    throw new ExternalApiException($"{apiName} returned an invalid response");

                return data;
            }
            catch (HttpRequestException)
            {
                throw new ExternalApiException($"{apiName} returned an invalid response");
            }
        }
    }
}

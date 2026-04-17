using System.Threading.Tasks;

namespace HNG_Stage_1.Services
{
    public interface IExternalApiService
    {
        Task<(GenderizeResponse Genderize, AgifyResponse Agify, NationalizeResponse Nationalize)> FetchAllDataAsync(string name);
    }

    public class GenderizeResponse
    {
        public int Count { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public double Probability { get; set; }
    }

    public class AgifyResponse
    {
        public int Count { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? Age { get; set; }
    }

    public class NationalizeResponse
    {
        public int Count { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<CountryData> Country { get; set; } = new List<CountryData>();
    }

    public class CountryData
    {
        public string Country_id { get; set; } = string.Empty;
        public double Probability { get; set; }
    }
}

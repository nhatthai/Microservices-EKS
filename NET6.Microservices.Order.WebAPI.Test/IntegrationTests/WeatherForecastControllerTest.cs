using Microsoft.VisualStudio.TestPlatform.TestHost;
using Net6.WebAPI.Test.Extentions;
using Net6.WebAPI.Test.Fixtures;
using System.Net;
using Xunit;

namespace NET6.WebAPI.Test.IntegrationTests
{
    public class WeatherForecastControllerTest : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _httpClient;

        public WeatherForecastControllerTest(CustomWebApplicationFactory<Program> factory)
        {
            _httpClient = factory.CreateClient();
        }

        [Fact]
        public async Task GetWeatherForecast_Return_200()
        {
            // Act
            var response = await _httpClient.GetAsync("/WeatherForecast");

            var result = await HttpContentExtensions.GetAsync<IEnumerable<WeatherForecast>>(response.Content);

            // Assert
            Xunit.Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Xunit.Assert.True(result.Count() > 0);

            var model = result.FirstOrDefault();
            Xunit.Assert.NotNull(model);
        }
    }
}
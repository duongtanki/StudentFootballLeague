using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace StudentFootballLeague.WebClient.Services
{
    public interface IApiClientService
    {
        Task<T?> GetAsync<T>(string uri);
        Task<(bool IsSuccess, TResponse? Data, string? ErrorMessage)> PostAsync<TRequest, TResponse>(string uri, TRequest content);
        Task<(bool IsSuccess, string? ErrorMessage)> PutAsync<TRequest>(string uri, TRequest content);
        Task<(bool IsSuccess, string? ErrorMessage)> DeleteAsync(string uri);
    }

    public class ApiClientService : IApiClientService
    {
        private readonly HttpClient _client;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiClientService(HttpClient client)
        {
            _client = client;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<T?> GetAsync<T>(string uri)
        {
            try
            {
                var response = await _client.GetAsync(uri);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
                }
            }
            catch
            {
                // Return null on failure
            }

            return default;
        }

        public async Task<(bool IsSuccess, TResponse? Data, string? ErrorMessage)> PostAsync<TRequest, TResponse>(string uri, TRequest content)
        {
            try
            {
                var response = await _client.PostAsJsonAsync(uri, content);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
                    return (true, data, null);
                }

                var error = await ExtractErrorMessage(response);
                return (false, default, error);
            }
            catch (Exception ex)
            {
                return (false, default, ex.Message);
            }
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> PutAsync<TRequest>(string uri, TRequest content)
        {
            try
            {
                var response = await _client.PutAsJsonAsync(uri, content);
                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var error = await ExtractErrorMessage(response);
                return (false, error);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> DeleteAsync(string uri)
        {
            try
            {
                var response = await _client.DeleteAsync(uri);
                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var error = await ExtractErrorMessage(response);
                return (false, error);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<string> ExtractErrorMessage(HttpResponseMessage response)
        {
            try
            {
                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var msgProp))
                {
                    return msgProp.GetString() ?? response.ReasonPhrase ?? "Lỗi không xác định";
                }
                return body;
            }
            catch
            {
                return response.ReasonPhrase ?? "Lỗi khi gọi Web API";
            }
        }
    }
}

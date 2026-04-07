using System.Text;
using System.Text.Json;
using ZabgcScheduleBot.API.DTOs;

namespace ZabgcScheduleBot.API
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;

        public ApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task GetUsers(UsersDto users)
        {
            var response = await GetAsync<UsersDto>("api/users");
        }

        public async Task GetUser(UsersDto users)
        {
            var response = await GetAsync<UsersDto>($"api/users{users.Id}");
        }
        public async Task CreateUserAsync(UsersDto users)
        {
            var response = await PostAsync("api/users", users);
        }

        public async Task DeleteUserAsync(int id)
        {
            var response = await DeleteAsync($"api/users/{id}");
        }
        public async Task UpdateUserAsync(UsersDto users)
        {
            var response = await PutAsync($"api/users/{users.Id}", users);
        }

        private async Task<bool> PostAsync<T>(string endpoint, T data)
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);
            return response.IsSuccessStatusCode;
        }

        private async Task<bool> GetAsync<T>(string endpoint)
        {
            var response = await _httpClient.GetAsync(endpoint);
            return response.IsSuccessStatusCode; 
        }

        private async Task<bool> PutAsync<T>(string endpoint, T data)
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(endpoint, content);
            return response.IsSuccessStatusCode;
        }

        private async Task<bool> DeleteAsync(string endpoint)
        {
            var response = await _httpClient.DeleteAsync(endpoint);
            return response.IsSuccessStatusCode;
        }
    }
}

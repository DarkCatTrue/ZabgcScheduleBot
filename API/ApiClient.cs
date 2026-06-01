using System.Net.Http.Json;
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
            _httpClient.BaseAddress = new Uri("http://localhost:5046/");
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-API-KEY", "qwerty");
        }
        public async Task GetUsers(UsersDto users)
        {
            var response = await GetAsync<UsersDto>("users");
        }

        public async Task GetUser(UsersDto users)
        {
            var response = await GetAsync<UsersDto>($"users{users.Id}");
        }
        public async Task CreateUserAsync(UsersDto users)
        {
            await PostAsync("users", users);
        }

        public async Task<bool> DeleteUserAsync(long chatId)
        {
            try
            {
                var user = await _httpClient.GetFromJsonAsync<UsersDto>($"users/chats/{chatId}");
                if (user == null) return false;

                await _httpClient.DeleteAsync($"users/{user.Id}");

            }
            catch 
            {
                Console.WriteLine("Объекта не существует в бд");
                return false;
            }
            return true;
        }
        public async Task UpdateUserAsync(UsersDto users)
        {
            var response = await PutAsync($"users/{users.Id}", users.DescriptionName);
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
    }
}

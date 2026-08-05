using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;
using Mate.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mate.AI
{
    /// <summary>
    /// Ollama chat provider. Talks to the Ollama /api/chat endpoint. HTTP is
    /// handled by an injected HttpClient so tests can use a fake handler.
    /// </summary>
    public class OllamaProvider : IAIService
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private readonly HttpClient _client;
        private string _systemPrompt = string.Empty;
        private readonly string _baseUrl;
        private readonly string _model;

        public bool IsAvailable
        {
            get
            {
                try
                {
                    using var response = _client.GetAsync($"{_baseUrl}/api/tags").Result;
                    return response.IsSuccessStatusCode;
                }
                catch
                {
                    return false;
                }
            }
        }

        public event Action<string> OnMessageReceived;
        public event Action<string> OnError;

        public OllamaProvider(IConfiguration config, IEventBus eventBus, HttpClient client = null)
        {
            _config = config;
            _eventBus = eventBus;
            _client = client ?? new HttpClient();
            _baseUrl = config.GetString("ai.baseUrl", "http://localhost:11434");
            _model = config.GetString("ai.model", "llama3.2");
        }

        public void SetSystemPrompt(string prompt) => _systemPrompt = prompt;
        public string GetSystemPrompt() => _systemPrompt;

        public Task<Result<string>> SendMessage(string message, CancellationToken ct = default)
        {
            var history = new List<ChatMessage>();
            if (!string.IsNullOrEmpty(_systemPrompt))
                history.Add(new ChatMessage("system", _systemPrompt));
            history.Add(new ChatMessage("user", message));
            return SendMessageWithHistory(history.ToArray(), ct);
        }

        public async Task<Result<string>> SendMessageWithHistory(ChatMessage[] history, CancellationToken ct = default)
        {
            try
            {
                var messages = new List<object>();
                foreach (var m in history)
                    messages.Add(new { role = m.Role, content = m.Content });

                var payload = new
                {
                    model = _model,
                    messages = messages,
                    stream = false,
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{_baseUrl}/api/chat", content, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var err = $"Ollama returned {(int)response.StatusCode}";
                    OnError?.Invoke(err);
                    return Result<string>.Fail(err);
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var doc = JObject.Parse(responseJson);
                var reply = doc["message"]?["content"]?.Value<string>() ?? string.Empty;

                OnMessageReceived?.Invoke(reply);
                _eventBus.Publish(new AiMessageEvent(reply));

                return Result<string>.Ok(reply);
            }
            catch (Exception ex)
            {
                var error = $"Ollama request failed: {ex.Message}";
                OnError?.Invoke(error);
                return Result<string>.Fail(error);
            }
        }
    }

    public record AiMessageEvent(string Content);
}
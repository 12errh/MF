# ADR-009: AI Architecture

## Status
Accepted

## Context
The current AI integration uses two libraries: LLMUnity (local LLM) and ollama-unity (Ollama API). Both are tightly coupled to Unity and hardcoded to specific models.

## Decision
Abstract AI behind a provider-based architecture with pluggable backends.

## Design

### AI Service Interface
```csharp
public interface IAIService
{
    Task<string> SendMessage(string message, CancellationToken ct = default);
    Task<string> SendMessageWithHistory(ChatMessage[] history, CancellationToken ct = default);
    void SetSystemPrompt(string prompt);
    string GetSystemPrompt();
    AIProvider[] GetAvailableProviders();
    bool IsAvailable { get; }
    event Action<string> OnMessageReceived;
    event Action<string> OnError;
}

public record ChatMessage(string Role, string Content);

public enum AIProvider
{
    Ollama,
    OpenAI,
    LocalLLM
}
```

### Provider Implementations
```csharp
// Ollama (HTTP API)
public class OllamaProvider : IAIService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;
    
    public async Task<string> SendMessage(string message, CancellationToken ct)
    {
        var request = new { model = _model, messages = new[] { new { role = "user", content = message } } };
        var response = await _http.PostAsJsonAsync($"{_baseUrl}/api/chat", request, ct);
        // Parse streaming response
    }
}

// LLMUnity (local, loads GGUF)
public class LocalLLMProvider : IAIService
{
    private readonly LLMCharacter _character;
    // Wraps existing LLMUnity integration
}
```

### Configuration
```toml
[ai]
enabled = true
provider = "ollama"
model = "phi3:mini"
base_url = "http://localhost:11434"    # For Ollama
context_length = 4096
temperature = 0.7
max_tokens = 2048
```

### Personality System
```toml
# config/personality.toml
[personality]
name = "Luna"
greeting = "Hello! I'm Luna, your desktop companion."
traits = ["friendly", "curious", "playful"]

[personality.behaviors]
idle = "I'll sit quietly and watch you work."
dragged = "Hey! That tickles!"
danced = "Ooh, music! Let me dance along!"
```

## Migration Path
1. Phase 1: Wrap existing LLMUnity + Ollama behind IAIService
2. Phase 2: Add OpenAI provider
3. Phase 3: Add personality system
4. Phase 4: Add memory/context management

## Rationale
1. **Provider flexibility** — Users choose their AI backend
2. **Testability** — Mock providers for testing
3. **Future-proof** — New providers added without changing consumers
4. **Configuration-driven** — No code changes for model switching

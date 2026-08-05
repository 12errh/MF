using System;
using System.Threading;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;

namespace Mate.Interfaces
{
    /// <summary>Pluggable AI provider (Ollama, ADR-009).</summary>
    public interface IAIService
    {
        Task<Result<string>> SendMessage(string message, CancellationToken ct = default);
        Task<Result<string>> SendMessageWithHistory(ChatMessage[] history, CancellationToken ct = default);
        void SetSystemPrompt(string prompt);
        string GetSystemPrompt();
        bool IsAvailable { get; }
        event Action<string> OnMessageReceived;
        event Action<string> OnError;
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Mate.AI;
using Mate.Core;
using Mate.Core.Models;
using Mate.Interfaces;
using NUnit.Framework;

[TestFixture]
public class AIServiceTests
{
    private MateContext _ctx;
    private FakeHttpHandler _http;

    [SetUp]
    public void SetUp()
    {
        _ctx = new MateContext();
        var config = new MockConfig();
        config.Set("ai.provider", "ollama");
        config.Set("ai.model", "llama3.2");
        config.Set("ai.baseUrl", "http://localhost:11434");
        _ctx.RegisterSingleton<IConfiguration>(config);
        _ctx.RegisterSingleton<IEventBus>(new SimpleEventBus());
        _http = new FakeHttpHandler();
    }

    [TearDown]
    public void TearDown() => _ctx.Dispose();

    private OllamaProvider CreateProvider()
    {
        return new OllamaProvider(_ctx.Resolve<IConfiguration>(), _ctx.Resolve<IEventBus>(),
            new HttpClient(_http));
    }

    [Test]
    public void OllamaProvider_ImplementsIAIService()
    {
        var svc = CreateProvider();
        Assert.IsInstanceOf<IAIService>(svc);
    }

    [Test]
    public void OllamaProvider_SetSystemPrompt_StoresPrompt()
    {
        var svc = CreateProvider();
        svc.SetSystemPrompt("You are a cute desktop pet.");
        Assert.AreEqual("You are a cute desktop pet.", svc.GetSystemPrompt());
    }

    [Test]
    public void OllamaProvider_GetSystemPrompt_DefaultIsEmpty()
    {
        var svc = CreateProvider();
        Assert.AreEqual(string.Empty, svc.GetSystemPrompt());
    }

    [Test]
    public void OllamaProvider_IsAvailable_False_WhenServerUnreachable()
    {
        _http.Response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var svc = CreateProvider();
        Assert.IsFalse(svc.IsAvailable);
    }

    [Test]
    public void OllamaProvider_IsAvailable_True_WhenServerResponds()
    {
        _http.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"models\":[]}")
        };
        var svc = CreateProvider();
        Assert.IsTrue(svc.IsAvailable);
    }

    [Test]
    public async Task OllamaProvider_SendMessage_ReturnsReply()
    {
        _http.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\":{\"role\":\"assistant\",\"content\":\"Hello there!\"}}")
        };

        var bus = _ctx.Resolve<IEventBus>();
        bool eventFired = false;
        string eventContent = null;
        bus.Subscribe<AiMessageEvent>(e => { eventFired = true; eventContent = e.Content; });

        var svc = CreateProvider();
        var result = await svc.SendMessage("hello", CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("Hello there!", result.Value);
        Assert.IsTrue(eventFired);
        Assert.AreEqual("Hello there!", eventContent);
    }

    [Test]
    public async Task OllamaProvider_SendMessage_IncludesSystemPrompt()
    {
        _http.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}")
        };

        var svc = CreateProvider();
        svc.SetSystemPrompt("You are Luna.");
        await svc.SendMessage("hi", CancellationToken.None);

        Assert.IsTrue(_http.LastRequestJson.Contains("\"role\":\"system\""));
        Assert.IsTrue(_http.LastRequestJson.Contains("You are Luna."));
    }

    [Test]
    public async Task OllamaProvider_OnError_WhenServerFails()
    {
        _http.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        string error = null;
        var svc = CreateProvider();
        svc.OnError += e => error = e;

        var result = await svc.SendMessage("hello", CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNotNull(error);
    }

    [Test]
    public async Task OllamaProvider_SendMessageWithHistory_SendsAllMessages()
    {
        _http.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}")
        };

        var svc = CreateProvider();
        var history = new[]
        {
            new ChatMessage("system", "sys"),
            new ChatMessage("user", "u1"),
            new ChatMessage("assistant", "a1"),
            new ChatMessage("user", "u2"),
        };
        var result = await svc.SendMessageWithHistory(history, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(_http.LastRequestJson.Contains("\"role\":\"assistant\""));
        Assert.AreEqual(4, _http.LastRequestMessagesCount);
    }

    private class FakeHttpHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\":{\"role\":\"assistant\",\"content\":\"\"}}")
        };
        public string LastRequestJson;
        public int LastRequestMessagesCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequestJson = request.Content?.ReadAsStringAsync().Result ?? "";
            LastRequestMessagesCount = 0;
            if (!string.IsNullOrEmpty(LastRequestJson))
            {
                var doc = Newtonsoft.Json.Linq.JObject.Parse(LastRequestJson);
                LastRequestMessagesCount = doc["messages"]?.Count() ?? 0;
            }
            return Task.FromResult(Response);
        }
    }

    private class MockConfig : IConfiguration
    {
        private readonly Dictionary<string, object> _v = new();
        public float GetFloat(string k, float d) => _v.TryGetValue(k, out var v) && v is float f ? f : d;
        public int GetInt(string k, int d) => _v.TryGetValue(k, out var v) && v is int i ? i : d;
        public string GetString(string k, string d) => _v.TryGetValue(k, out var v) && v is string s ? s : d;
        public bool GetBool(string k, bool d) => _v.TryGetValue(k, out var v) && v is bool b ? b : d;
        public void Set(string k, object v) => _v[k] = v;
        public void Save() { }
        public void Reload() { }
    }
}
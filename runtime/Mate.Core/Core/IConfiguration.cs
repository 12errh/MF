namespace Mate.Core
{
    public interface IConfiguration
    {
        float GetFloat(string key, float defaultValue);
        int GetInt(string key, int defaultValue);
        string GetString(string key, string defaultValue);
        bool GetBool(string key, bool defaultValue);
        void Set(string key, object value);
        void Save();
        void Reload();
    }
}
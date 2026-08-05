using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;

namespace Mate.Interfaces
{
    /// <summary>System integration: tray icon and desktop notifications.</summary>
    public interface ISystemService
    {
        bool IsSupported { get; }
        Task<Result> ShowTrayIcon(string iconPath, string tooltip);
        Task<Result> HideTrayIcon();
        Task<Result> ShowNotification(string title, string message);
    }
}
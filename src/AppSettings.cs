using System.Text.Json;
using LanAudio.Shared;

namespace LanAudio.Sender;

/// <summary>%APPDATA%\LanAudio\sender.json に保存する設定</summary>
sealed class AppSettings
{
    /// <summary>受信側（デスクトップPC）の IP アドレスまたはホスト名</summary>
    public string ServerHost { get; set; } = "";
    public int Port { get; set; } = AudioPacket.DefaultPort;
    /// <summary>アプリ起動時に送信を開始する</summary>
    public bool AutoStart { get; set; } = true;

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LanAudio", "sender.json");

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

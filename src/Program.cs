// ノートPC側（クライアント）: PCの再生音を取り込み、UDPで受信側PCへ送る
// 通知領域（タスクバー右下）に常駐し、右クリックメニューから操作する
// 使い方: AudioSender.exe [受信側PCのIPアドレス] [ポート]   ※省略時は保存済みの設定
namespace LanAudio.Sender;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\LanAudio.Sender", out bool first);
        if (!first)
        {
            MessageBox.Show("LanAudio 送信はすでに起動しています。\nタスクバー右下の通知領域にあるアイコンから操作してください。",
                "LanAudio 送信", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(new Font("Yu Gothic UI", 9f));
        Application.Run(new TrayApp(args));
    }
}

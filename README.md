# lanaudio-client（送信側／ノートPC）

PCの再生音を WASAPI ループバックで取り込み、16bit PCM に変換して UDP でデスクトップPC（[lanaudio-server](https://github.com/mazin125533/lanaudio-server)）へ送ります。
タスクバー右下の通知領域に常駐するアプリです。

## 使い方
1. [Releases](https://github.com/mazin125533/lanaudio-client/releases) から `AudioSender.exe` をダウンロードして起動
2. 初回は設定画面が開くので、受信側PCの IP アドレスを入力して OK
3. 通知領域にオレンジのスピーカーのアイコンが出て、PCで鳴っている音が送られます

| アイコン | 状態 |
|---|---|
| グレー | 停止中 |
| オレンジ | 待機中（再生音なし）／受信側が応答しない |
| オレンジ＋緑の丸 | 送信中 |

右クリックメニュー
- **送信を開始／停止**
- **設定...**（アイコンのダブルクリックでも開く）: 送信先 IP、ポート、起動時に自動で送信開始、Windows 起動時に起動
- **Windows 起動時に起動**

設定は `%APPDATA%\LanAudio\sender.json` に保存されます。
イヤホンを挿すなどで既定の再生デバイスが変わると自動で追従します。

- 48kHzステレオで約1.5Mbps
- ノートPCをミュートすると環境によっては送信も無音になるので、イヤホンを挿すなどで回避

## ビルド
.NET 8 SDK を入れて `build.bat` を実行 → `publish\AudioSender.exe`

アイコン（`src/app.ico`）は `src/IconArt.cs` の描画から `tools/make-icon.ps1` で生成しています。

## パケット形式
`src/AudioPacket.cs` 参照（サーバー側と同じファイル。変更する場合は両方そろえる）。

## ライセンス

[MIT License](LICENSE)。自由に使用・改変・再配布できます。

音声処理に [NAudio](https://github.com/naudio/NAudio)（MIT License）を使用しています。

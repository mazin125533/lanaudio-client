# lanaudio-client（送信側／ノートPC）

PCの再生音を WASAPI ループバックで取り込み、16bit PCM に変換して UDP でデスクトップPC（[lanaudio-server](../lanaudio-server)）へ送ります。

## ビルド
.NET 8 SDK を入れて `build.bat` を実行 → `publish\AudioSender.exe`

## 使い方
```
AudioSender.exe <サーバーのIPアドレス> [ポート=50005]
```

- 48kHzステレオで約1.5Mbps
- ノートPCをミュートすると環境によっては送信も無音になるので、イヤホンを挿すなどで回避

## パケット形式
`src/AudioPacket.cs` 参照（サーバー側と同じファイル。変更する場合は両方そろえる）。

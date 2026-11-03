// ノートPC側（クライアント）: PCの再生音をWASAPIループバックで取り込み、UDPで送信する
// 使い方: AudioSender.exe <受信側PCのIPアドレス> [ポート]
using System.Buffers.Binary;
using System.Net.Sockets;
using LanAudio.Shared;
using NAudio.Wave;

if (args.Length < 1)
{
    Console.WriteLine("使い方: AudioSender <受信側IPアドレス> [ポート(既定 50005)]");
    return;
}

string host = args[0];
int port = args.Length >= 2 ? int.Parse(args[1]) : AudioPacket.DefaultPort;

using var udp = new UdpClient();
udp.Connect(host, port);

using var capture = new WasapiLoopbackCapture(); // 既定の再生デバイスの音を取り込む
var fmt = capture.WaveFormat;

// IEEE float の SubFormat GUID (KSDATAFORMAT_SUBTYPE_IEEE_FLOAT)
var floatGuid = new Guid("00000003-0000-0010-8000-00aa00389b71");
bool isFloat = fmt.Encoding == WaveFormatEncoding.IeeeFloat
               || (fmt is WaveFormatExtensible ext && ext.SubFormat == floatGuid);
int inCh = fmt.Channels;
int outCh = Math.Min(inCh, 2);            // 5.1ch等は前方L/Rだけ送る
int bytesPerSample = fmt.BitsPerSample / 8;
int inFrameBytes = fmt.BlockAlign;
int outFrameBytes = outCh * 2;
int chunkBytes = AudioPacket.MaxPayload - AudioPacket.MaxPayload % outFrameBytes;

Console.WriteLine($"取込フォーマット: {fmt.SampleRate}Hz {inCh}ch {fmt.BitsPerSample}bit {(isFloat ? "float" : "PCM")}");
Console.WriteLine($"送信先: {host}:{port}  ({fmt.SampleRate}Hz {outCh}ch 16bit)  Ctrl+C で終了");

byte[] pcm = Array.Empty<byte>();
byte[] packet = new byte[AudioPacket.HeaderSize + AudioPacket.MaxPayload];
uint seq = 0;
long sentPackets = 0;

capture.DataAvailable += (_, e) =>
{
    int frames = e.BytesRecorded / inFrameBytes;
    if (frames == 0) return;
    int outBytes = frames * outFrameBytes;
    if (pcm.Length < outBytes) pcm = new byte[outBytes];

    // 16bit PCM へ変換
    var src = e.Buffer.AsSpan(0, frames * inFrameBytes);
    var dst = pcm.AsSpan(0, outBytes);
    int o = 0;
    for (int f = 0; f < frames; f++)
    {
        int frameOffset = f * inFrameBytes;
        for (int c = 0; c < outCh; c++)
        {
            var s = src.Slice(frameOffset + c * bytesPerSample, bytesPerSample);
            short v = ToInt16(s, isFloat, bytesPerSample);
            BinaryPrimitives.WriteInt16LittleEndian(dst.Slice(o, 2), v);
            o += 2;
        }
    }

    // MTU以下のサイズに分割して送信
    for (int pos = 0; pos < outBytes; pos += chunkBytes)
    {
        int len = Math.Min(chunkBytes, outBytes - pos);
        int n = AudioPacket.Write(packet, outCh, fmt.SampleRate, seq++, dst.Slice(pos, len));
        try { udp.Send(packet, n); sentPackets++; }
        catch (SocketException ex) { Console.WriteLine($"送信エラー: {ex.Message}"); }
    }
};

var stopped = new ManualResetEventSlim();
capture.RecordingStopped += (_, e) =>
{
    if (e.Exception != null) Console.WriteLine($"取込停止: {e.Exception.Message}");
    stopped.Set();
};
Console.CancelKeyPress += (_, e) => { e.Cancel = true; capture.StopRecording(); };

capture.StartRecording();
while (!stopped.Wait(1000))
{
    Console.Write($"\r送信パケット数: {sentPackets}   ");
}
Console.WriteLine("\n終了しました");

static short ToInt16(ReadOnlySpan<byte> s, bool isFloat, int bytesPerSample)
{
    if (isFloat && bytesPerSample == 4)
    {
        float x = BinaryPrimitives.ReadSingleLittleEndian(s);
        x = Math.Clamp(x, -1f, 1f);
        return (short)(x * 32767f);
    }
    return bytesPerSample switch
    {
        2 => BinaryPrimitives.ReadInt16LittleEndian(s),
        3 => (short)(s[1] | (s[2] << 8)),                       // 24bit → 上位16bit
        4 => (short)(BinaryPrimitives.ReadInt32LittleEndian(s) >> 16),
        _ => 0
    };
}

// PCの再生音をWASAPIループバックで取り込み、16bit PCM に変換してUDPで送信する
using System.Buffers.Binary;
using System.Net.Sockets;
using LanAudio.Shared;
using NAudio.Wave;

namespace LanAudio.Sender;

public sealed class SenderEngine : IDisposable
{
    // IEEE float の SubFormat GUID (KSDATAFORMAT_SUBTYPE_IEEE_FLOAT)
    static readonly Guid FloatGuid = new("00000003-0000-0010-8000-00aa00389b71");

    readonly UdpClient udp;
    readonly WasapiLoopbackCapture capture;   // 既定の再生デバイスの音を取り込む
    readonly bool isFloat;
    readonly int outCh, sampleRate, bytesPerSample, inFrameBytes, outFrameBytes, chunkBytes;
    readonly byte[] packet = new byte[AudioPacket.HeaderSize + AudioPacket.MaxPayload];
    byte[] pcm = Array.Empty<byte>();
    uint seq;
    long sentPackets, lastSendTicks, lastErrorTicks = long.MinValue / 2;
    volatile bool disposing;

    public string Destination { get; }
    /// <summary>送信フォーマット（"48000Hz 2ch 16bit"）</summary>
    public string Format { get; }
    public long SentPackets => Interlocked.Read(ref sentPackets);
    /// <summary>直近1秒以内に送信した（＝PCで音が鳴っている）</summary>
    public bool IsSending => Environment.TickCount64 - Interlocked.Read(ref lastSendTicks) < 1000;
    /// <summary>直近3秒以内に送信エラーがあった（受信側が起動していない等）</summary>
    public bool HasRecentError => Environment.TickCount64 - Interlocked.Read(ref lastErrorTicks) < 3000;

    /// <summary>取り込みが止まったとき（デバイスの切断など。別スレッドから呼ばれる）</summary>
    public event Action<Exception?>? Stopped;

    public SenderEngine(string host, int port)
    {
        Destination = $"{host}:{port}";
        udp = new UdpClient();
        try
        {
            udp.Connect(host, port);
            capture = new WasapiLoopbackCapture();
        }
        catch
        {
            udp.Dispose();
            throw;
        }

        var fmt = capture.WaveFormat;
        isFloat = fmt.Encoding == WaveFormatEncoding.IeeeFloat
                  || (fmt is WaveFormatExtensible ext && ext.SubFormat == FloatGuid);
        outCh = Math.Min(fmt.Channels, 2);            // 5.1ch等は前方L/Rだけ送る
        sampleRate = fmt.SampleRate;
        bytesPerSample = fmt.BitsPerSample / 8;
        inFrameBytes = fmt.BlockAlign;
        outFrameBytes = outCh * 2;
        chunkBytes = AudioPacket.MaxPayload - AudioPacket.MaxPayload % outFrameBytes;
        Format = $"{sampleRate}Hz {outCh}ch 16bit";

        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += (_, e) => { if (!disposing) Stopped?.Invoke(e.Exception); };
        try
        {
            capture.StartRecording();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        disposing = true;
        capture.Dispose();
        udp.Dispose();
    }

    void OnDataAvailable(object? sender, WaveInEventArgs e)
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
                BinaryPrimitives.WriteInt16LittleEndian(dst.Slice(o, 2), ToInt16(s, isFloat, bytesPerSample));
                o += 2;
            }
        }

        // MTU以下のサイズに分割して送信
        for (int pos = 0; pos < outBytes; pos += chunkBytes)
        {
            int len = Math.Min(chunkBytes, outBytes - pos);
            int n = AudioPacket.Write(packet, outCh, sampleRate, seq++, dst.Slice(pos, len));
            try
            {
                udp.Send(packet, n);
                Interlocked.Increment(ref sentPackets);
                Interlocked.Exchange(ref lastSendTicks, Environment.TickCount64);
            }
            catch (SocketException)
            {
                Interlocked.Exchange(ref lastErrorTicks, Environment.TickCount64);
            }
            catch (ObjectDisposedException)
            {
                return;   // 停止処理中
            }
        }
    }

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
}

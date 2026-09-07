using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// WebRTC Signaling Sunucusu
/// - ws://localhost:8080/   → WebSocket signaling (offer/answer/ICE)
/// - http://localhost:8080/ → Frontend dosyalarını sunar (kamera izni için gerekli)
/// </summary>
class SignalingServer
{
    // Bağlı istemcileri saklar: clientId -> WebSocket
    private static ConcurrentDictionary<string, WebSocket> clients = new();
    private static int clientCounter = 0;

    // Frontend klasörünün yolu (SignalingServer.exe'nin yanındaki ../frontend)
    private static string frontendKlasor = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "frontend")
    );

    static async Task Main(string[] args)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");
        listener.Start();

        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║        WebRTC Signaling + Dosya Sunucusu         ║");
        Console.WriteLine("╠══════════════════════════════════════════════════╣");
        Console.WriteLine("║  Tarayıcıda şu adresi aç:                        ║");
        Console.WriteLine("║  → http://localhost:8080/                        ║");
        Console.WriteLine("║                                                  ║");
        Console.WriteLine("║  İki sekme aç, kameranı aç ve test et!           ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
        Console.WriteLine();

        while (true)
        {
            var context = await listener.GetContextAsync();

            if (context.Request.IsWebSocketRequest)
            {
                _ = HandleClientAsync(context);
            }
            else
            {
                _ = ServeFileAsync(context);
            }
        }
    }

    /// <summary>
    /// HTTP isteklerini karşılar — frontend/index.html dosyasını sunar.
    /// Kamera izninin çalışması için http:// üzerinden açılması şart.
    /// </summary>
    static async Task ServeFileAsync(HttpListenerContext context)
    {
        try
        {
            string urlYolu = context.Request.Url?.AbsolutePath ?? "/";
            if (urlYolu == "/") urlYolu = "/index.html";

            string dosyaYolu = Path.Combine(frontendKlasor, urlYolu.TrimStart('/'));

            if (!File.Exists(dosyaYolu))
            {
                context.Response.StatusCode = 404;
                byte[] hataBytes = Encoding.UTF8.GetBytes("404 - Dosya bulunamadı: " + urlYolu);
                await context.Response.OutputStream.WriteAsync(hataBytes);
                context.Response.Close();
                return;
            }

            string uzanti = Path.GetExtension(dosyaYolu).ToLower();
            context.Response.ContentType = uzanti switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js"   => "application/javascript",
                ".css"  => "text/css",
                ".png"  => "image/png",
                ".jpg"  => "image/jpeg",
                _       => "application/octet-stream"
            };

            byte[] icerik = await File.ReadAllBytesAsync(dosyaYolu);
            context.Response.ContentLength64 = icerik.Length;
            await context.Response.OutputStream.WriteAsync(icerik);
            context.Response.Close();

            Console.WriteLine($"[HTTP] {context.Request.HttpMethod} {urlYolu}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Dosya sunucu hatası: {ex.Message}");
            try { context.Response.StatusCode = 500; context.Response.Close(); } catch { }
        }
    }

    static async Task HandleClientAsync(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var ws = wsContext.WebSocket;

        // Benzersiz bir ID ata
        string clientId = "Kullanici_" + Interlocked.Increment(ref clientCounter);
        clients[clientId] = ws;

        Console.WriteLine($"[+] Yeni bağlantı: {clientId} (Toplam: {clients.Count})");

        // İstemciye kendi ID'sini gönder
        await SendAsync(ws, $"{{\"type\":\"welcome\",\"clientId\":\"{clientId}\"}}");

        // Diğer istemcilere yeni biri geldiğini bildir
        await BroadcastAsync($"{{\"type\":\"peer-joined\",\"peerId\":\"{clientId}\"}}", excludeId: clientId);

        // Mevcut kullanıcıların listesini gönder
        var peerList = string.Join(",", System.Linq.Enumerable
            .Where(clients.Keys, k => k != clientId)
            .Select(k => $"\"{k}\""));
        await SendAsync(ws, $"{{\"type\":\"peer-list\",\"peers\":[{peerList}]}}");

        var buffer = new byte[4096];

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                string message = Encoding.UTF8.GetString(ms.ToArray());
                Console.WriteLine($"[{clientId}] -> {message.Substring(0, Math.Min(message.Length, 80))}...");

                // Mesajı ayrıştır ve hedef istemciye ilet
                await RouteMessageAsync(clientId, message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Hata ({clientId}): {ex.Message}");
        }
        finally
        {
            clients.TryRemove(clientId, out _);
            Console.WriteLine($"[-] Bağlantı kesildi: {clientId} (Toplam: {clients.Count})");
            await BroadcastAsync($"{{\"type\":\"peer-left\",\"peerId\":\"{clientId}\"}}");

            if (ws.State != WebSocketState.Closed)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bağlantı kapatıldı", CancellationToken.None);
        }
    }

    /// <summary>
    /// Gelen mesajı ayrıştırarak ilgili hedefe yönlendirir.
    /// Beklenen format: { "to": "client_X", "type": "offer/answer/candidate", ... }
    /// </summary>
    static async Task RouteMessageAsync(string fromId, string message)
    {
        try
        {
            // Basit JSON ayrıştırma: "to" alanını bul
            string? targetId = ExtractJsonField(message, "to");

            if (targetId != null && clients.TryGetValue(targetId, out var targetWs))
            {
                // "from" alanı ekleyerek hedefe ilet
                string forwarded = message.TrimEnd('}') + $",\"from\":\"{fromId}\"}}";
                Console.WriteLine($"  => {fromId} -> {targetId}: {forwarded}");
                await SendAsync(targetWs, forwarded);
            }
            else
            {
                Console.WriteLine($"  [!] Hedef bulunamadı veya belirtilmedi: {targetId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Yönlendirme hatası: {ex.Message}");
        }
    }

    static async Task SendAsync(WebSocket ws, string message)
    {
        if (ws.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(message);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    static async Task BroadcastAsync(string message, string? excludeId = null)
    {
        foreach (var (id, ws) in clients)
        {
            if (id == excludeId) continue;
            await SendAsync(ws, message);
        }
    }

    /// <summary>
    /// JSON string içinden belirtilen alanın değerini çeker (string değerler için).
    /// Örnek: {"to":"client_2"} -> "client_2"
    /// </summary>
    static string? ExtractJsonField(string json, string field)
    {
        string search = $"\"{field}\":\"";
        int start = json.IndexOf(search);
        if (start < 0) return null;
        start += search.Length;
        int end = json.IndexOf('"', start);
        if (end < 0) return null;
        return json.Substring(start, end - start);
    }
}

# WebRTC Metin Sohbet Uygulaması

## Proje Yapısı

```
webrtc-app/
├── SignalingServer/
│   ├── SignalingServer.cs       ← C# WebSocket Signaling Sunucusu
│   └── SignalingServer.csproj
└── frontend/
    └── index.html               ← WebRTC DataChannel Sohbet Arayüzü
```

---

## Nasıl Çalışır?

### Mimari

```
Tarayıcı A                 Signaling Sunucusu (C#)             Tarayıcı B
    |                              |                                |
    |── WebSocket bağlantısı ─────>|<──── WebSocket bağlantısı ────|
    |                              |                                |
    |── offer (SDP) ──────────────>|── offer (SDP) ───────────────>|
    |<─ answer (SDP) ─────────────|<─ answer (SDP) ────────────────|
    |── ICE candidates ───────────>|── ICE candidates ─────────────>|
    |<─ ICE candidates ───────────|<─ ICE candidates ───────────────|
    |                              |                                |
    |<═══════════ RTCDataChannel (P2P direkt bağlantı) ════════════>|
    |             (Signaling sunucusu artık devrede değil)          |
```

### Kullanılan WebRTC API'leri

| API                   | Kullanım Amacı                              |
|-----------------------|---------------------------------------------|
| `RTCPeerConnection`   | P2P bağlantı yönetimi, ICE/SDP işlemleri   |
| `RTCDataChannel`      | Metin mesajlarının P2P iletimi              |
| `RTCSessionDescription` | SDP offer/answer oluşturma               |
| `RTCIceCandidate`     | NAT geçişi için ICE adayları               |

---

## Kurulum ve Çalıştırma

### Gereksinimler
- .NET 8 SDK (https://dotnet.microsoft.com/download)
- Chrome veya Firefox tarayıcı

---

### 1. Signaling Sunucusunu Başlat

```bash
cd SignalingServer
dotnet run
```

Çıktı:
```
Signaling sunucusu başlatıldı: ws://localhost:8080/
```

---

### 2. Frontend'i Aç

`frontend/index.html` dosyasını **iki ayrı tarayıcı sekmesinde** aç.

> **Not:** Dosyayı direkt açabilirsiniz (`file:///...`).  
> İsteğe bağlı olarak basit bir HTTP sunucusu da kullanabilirsiniz:
> ```bash
> cd frontend
> python -m http.server 3000
> # Sonra: http://localhost:3000
> ```

---

### 3. Test Et

1. İlk sekme açılır → sunucuya bağlanır → `client_1` ID'si alır
2. İkinci sekme açılır → `client_2` ID'si alır
3. Birinci sekmede açılan listeden `client_2`'yi seç → **Bağlan** butonuna tıkla
4. WebRTC P2P bağlantısı kurulur
5. Mesajlaşmaya başla! 🎉

---

## Bileşenler

### `SignalingServer.cs` (C#)

- `HttpListener` ile WebSocket sunucusu
- Her bağlanan istemciye benzersiz ID (`client_1`, `client_2`, ...) atar
- `peer-list`, `peer-joined`, `peer-left` mesajları ile kullanıcı listesini yönetir
- `offer`, `answer`, `candidate` mesajlarını doğru hedefe yönlendirir
- Birden fazla bağlantıyı `ConcurrentDictionary` ile thread-safe yönetir

### `index.html` (JavaScript/WebRTC)

- `RTCPeerConnection` ile STUN sunucusu üzerinden ICE adayı toplar
- Bağlantıyı başlatan taraf `createDataChannel()` ile kanal açar
- Diğer taraf `ondatachannel` olayı ile kanalı alır
- Tüm sohbet P2P olarak iletilir (signaling sunucusu sadece kurulum içindir)

---

## Notlar

- STUN sunucusu: `stun.l.google.com:19302` (ücretsiz, Google)
- Aynı ağdaki iki kullanıcı için TURN sunucusu **gerekmez**
- Farklı ağlar / internet üzerinde test için TURN sunucusu gerekebilir

# WebRTC Peer-to-Peer Chat

A browser-based, real-time text chat application that establishes peer-to-peer communication through WebRTC DataChannels. A lightweight C# WebSocket signaling server coordinates the initial connection; messages are then exchanged directly between peers.

> **Türkçe özet:** WebRTC DataChannel ile tarayıcılar arasında doğrudan (P2P) metin mesajlaşması sağlayan gerçek zamanlı sohbet uygulaması. C# WebSocket signaling sunucusu yalnızca bağlantının kurulmasını koordine eder.

## Highlights

- WebRTC `RTCPeerConnection` and `RTCDataChannel` integration
- WebSocket-based signaling server written in C# / .NET 8
- SDP offer/answer and ICE candidate exchange
- Concurrent, thread-safe client management
- Two-browser local test workflow

## Architecture

```text
Browser A ── WebSocket signaling ── C# Signaling Server ── WebSocket signaling ── Browser B
    └──────────────────────── WebRTC DataChannel (P2P chat) ────────────────────────┘
```

## Run locally

> **Türkçe:** Yerelde çalıştırmak için önce signaling sunucusunu, ardından frontend'i başlatın ve uygulamayı iki tarayıcı sekmesinde açın.

### 1. Start the signaling server

```bash
cd SignalingServer
dotnet run
```

The server listens on `ws://localhost:8080/`.

### 2. Serve the frontend

```bash
cd frontend
python -m http.server 3000
```

Open `http://localhost:3000` in two browser tabs, choose the other peer, and connect.

## Tech stack

> **Türkçe:** Projede C#/.NET 8, WebSocket, HTML/CSS/JavaScript ve WebRTC teknolojileri kullanılmıştır.

- C# / .NET 8
- WebSocket (`HttpListener`)
- HTML, CSS and JavaScript
- WebRTC APIs

## Notes

The application uses Google's public STUN server for ICE gathering. For reliable connections between restrictive networks, a TURN server would be needed in a production deployment.

For the original Turkish coursework documentation, see [Read/README.md](Read/README.md).

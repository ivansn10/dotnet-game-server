# High-Performance WebRTC Signaling Infrastructure 🚀

A robust, asynchronous signaling gateway built with **.NET 8** to facilitate real-time, peer-to-peer communication. This system handles WebSocket-based signaling for WebRTC handshakes, designed for low-latency and high-concurrency gaming environments.

---

### 🛠️ Key Technical Features
- **Real-Time Signaling:** Leveraging WebSockets for full-duplex communication during the WebRTC offer/answer exchange.
- **Asynchronous Architecture:** Built using C# Task Parallel Library (TPL) to ensure non-blocking I/O and high scalability.
- **Clean Infrastructure:** Decoupled signaling logic from the transport layer, allowing for easy integration with different game engines (Unity, Godot) or web clients.
- **Security Focused:** Designed to support secure WebSockets (WSS) and peer authentication.

---

### 🏗️ System Architecture

```mermaid
sequenceDiagram
    participant PeerA as Player A (Client)
    participant Server as .NET Signaling Server
    participant PeerB as Player B (Client)
    
    PeerA->>Server: WebSocket Connection (Join Room)
    PeerB->>Server: WebSocket Connection (Join Room)
    PeerA->>Server: Send WebRTC Offer
    Server->>PeerB: Forward WebRTC Offer
    PeerB->>Server: Send WebRTC Answer
    Server->>PeerA: Forward WebRTC Answer
    Note over PeerA,PeerB: P2P Connection Established (DataChannel)

---
name: hal-layer
description: >
  Hướng dẫn viết code cho HAL (Hardware Abstraction Layer) trong project
  FCT-G6T WinForms. Dùng skill này bất cứ khi nào cần tạo mới, chỉnh sửa,
  hoặc review code thuộc namespace FCT.G6T.HAL — bao gồm CaptureLoop,
  SerialWrapper, GPIO driver wrapper, hoặc bất kỳ class nào bọc trực tiếp
  driver/SDK phần cứng. Trigger khi user nhắc đến: "HAL", "CaptureLoop",
  "SerialWrapper", "bọc driver", "wrap SDK", "camera loop", "UART raw", "QR COM".
---

# Kỹ năng HAL Layer

## 1. Mục đích & Ranh giới

HAL là layer thấp nhất, bọc trực tiếp driver và SDK phần cứng.

**HAL được phép:**
- Import thư viện driver cụ thể (OpenCvSharp, System.IO.Ports, SDK vendor)
- Expose hardware thông qua interface thuần C# lên Infrastructure

**HAL bị cấm:**
- Import bất kỳ namespace Application hoặc Domain nào
- Chứa business logic hoặc test-case logic
- Gọi ILogger trực tiếp — chỉ throw exception, layer trên log

Namespace bắt buộc: `FCT.G6T.HAL.{SubModule}`
Ví dụ: `FCT.G6T.HAL.Camera`, `FCT.G6T.HAL.Serial`

---

## 2. CaptureLoop

> Chi tiết pattern xem: `references/capture-loop.md`

**Checklist nhanh khi viết CaptureLoop:**
- [ ] Chạy trên background `Task`, không dùng `Thread`
- [ ] Dùng `CancellationToken` — không `Thread.Sleep`
- [ ] `FrameBuffer` là `ConcurrentQueue<Bitmap>`, giới hạn 3 frame
- [ ] `Dispose` bitmap cũ trước khi enqueue frame mới
- [ ] `StartAsync(CancellationToken)` / `StopAsync()` là public API duy nhất
- [ ] Không raise event trực tiếp lên UI — dùng event, để Infrastructure subscribe

**Khung mẫu:**
```csharp
namespace FCT.G6T.HAL.Camera;

public sealed class CaptureLoop : IAsyncDisposable
{
    private readonly ConcurrentQueue _buffer = new();
    private const int MAX_BUFFER = 3;
    private Task? _loopTask;

    public event EventHandler? FrameReady;

    public Task StartAsync(CancellationToken token) { ... }
    public async ValueTask DisposeAsync() { ... }

    private async Task RunLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // capture → enqueue → trim buffer → raise event
        }
    }
}

```
---

## 3. SerialWrapper

> Chi tiết xem: `references/serial-wrapper.md`

**Checklist nhanh:**
- [ ] Wrap `System.IO.Ports.SerialPort` — không expose type này ra ngoài HAL
- [ ] `OpenAsync` / `CloseAsync` / `SendAsync` / `ReceiveAsync`
- [ ] `ReceiveLineAsync` cho thiet bi serial text nhu QR scanner, doc den CR/LF
- [ ] Timeout qua `CancellationToken`, không dùng `SerialPort.ReadTimeout`
- [ ] Throw `HardwareException` (custom) khi port đóng đột ngột
- [ ] `ConfigureAwait(false)` trên mọi await

---

## 4. Hợp đồng lỗi

HAL **không log** — chỉ throw. Infrastructure và Application layer chịu trách nhiệm log.

| Tình huống | Exception phải throw |
|---|---|
| Port không mở được | `HardwareException("SerialPort {ComPort} failed to open")` |
| Camera disconnect | `HardwareException("Camera index {Index} disconnected")` |
| Read timeout | `TimeoutException` (để Orchestrator bắt và retry) |
| SDK trả lỗi không xác định | `HardwareException` bọc inner exception gốc |

Custom exception cho wrapper serial đặt tại: `FCT.G6T.HAL.Serial`, để HAL không import Application/Domain namespace.

---

## 5. Quy tắc Async (HAL cụ thể)

- Mọi public method có I/O: suffix `Async`, trả `Task` hoặc `ValueTask`
- `ConfigureAwait(false)` **bắt buộc** trên mọi `await` trong HAL
- Không tạo `CancellationTokenSource` mới — nhận token từ caller
- `IAsyncDisposable` thay vì `IDisposable` cho class có background task

---

## 6. Khi nào đọc file tham chiếu

| Bạn cần... | Đọc file |
|---|---|
| Implement hoặc sửa CaptureLoop | `references/capture-loop.md` |
| Implement hoặc sửa SerialWrapper / UART | `references/serial-wrapper.md` |
| Tra cứu / thêm exception type | `references/error-catalogue.md` |

---

## 7. Tiêu chí hoàn thành cho HAL class

- [ ] Namespace đúng `FCT.G6T.HAL.{SubModule}`
- [ ] Không import Application/Domain namespace
- [ ] Mọi public method có I/O là `async` + `ConfigureAwait(false)`
- [ ] Không `Console.WriteLine` / `Debug.Print`
- [ ] `Dispose` / `DisposeAsync` dọn sạch resource
- [ ] Có unit test tương ứng tại `tests/HAL.Tests/`

# Tham chiếu SerialWrapper

## Mục đích
Định nghĩa pattern sử dụng UART/SerialPort cho triển khai HAL.

## Pattern bắt buộc
- Bọc `System.IO.Ports.SerialPort` và không expose type này ra ngoài.
- Cung cấp API async: `OpenAsync`, `CloseAsync`, `SendAsync`, `ReceiveAsync`.
- Dùng `CancellationToken` cho timeout; tránh `SerialPort.ReadTimeout`.
- Dùng `ConfigureAwait(false)` cho mọi `await`.

## Xử lý lỗi
- Throw `HardwareException` khi cổng bị ngắt hoặc không còn hợp lệ.
- Dùng `TimeoutException` cho timeout đọc/ghi để cho phép retry.
- Không log trong HAL.

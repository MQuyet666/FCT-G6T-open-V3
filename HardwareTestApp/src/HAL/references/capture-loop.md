# Tham chiếu CaptureLoop

## Mục đích
Tài liệu hóa pattern CaptureLoop cho việc polling camera/SDK trong HAL.

## Pattern bắt buộc
- Chạy background `Task` với `CancellationToken`.
- Không dùng `Thread.Sleep`; dùng cơ chế chờ theo token hoặc delay tối thiểu khi cần.
- Buffer là `ConcurrentQueue<Bitmap>`, tối đa 3 frame.
- Dispose frame cũ trước khi enqueue frame mới.
- Event chỉ phát cho Infrastructure subscribe; không cập nhật UI tại đây.

## Hình dạng API
- `StartAsync(CancellationToken)`
- `StopAsync()` hoặc `DisposeAsync()`

## Xử lý lỗi
- Throw exception lên caller; không log trong HAL.

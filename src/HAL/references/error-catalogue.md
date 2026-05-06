# Danh mục lỗi

## Mục đích
Liệt kê exception tùy biến và thông điệp lỗi dùng trong HAL.

## Exceptions
- `HardwareException`: wrapper cho lỗi phần cứng/SDK.

## Thông điệp thường dùng
- `HardwareException("SerialPort {ComPort} failed to open")`
- `HardwareException("Camera index {Index} disconnected")`
- `HardwareException("SDK failure: {Message}")`

## Ghi chú
- HAL chỉ throw; Infrastructure/Application chịu trách nhiệm log.
- HAL không import `FCT.G6T.Application` hoặc `FCT.G6T.Domain`; exception riêng của wrapper serial nằm trong `FCT.G6T.HAL.Serial`.
- Dùng inner exception để giữ lỗi gốc từ SDK.

# Hướng dẫn Copilot

## Quy định dự án
- Repo dùng quy ước trong `Rule.md`: Clean Architecture 5 layer, UI không gọi trực tiếp `Infrastructure`/`HAL`, bắt buộc constructor injection, tránh hardcoded camera index/timeout, dùng `async`/`await` không `.Wait()`/`.Result`, và cập nhật UI camera qua `Invoke`.

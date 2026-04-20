
# Hardware Test WinForms — Coding Convention

**Version 1.0  |  C# .NET  |  WinForms + OpenCvSharp**

## 1. Kiến trúc & Layer
Project tuân theo Clean Architecture 5 layer. Mỗi layer chỉ được phép phụ thuộc vào layer bên trong nó.

- **Presentation**: chỉ chứa WinForms, UserControl. Không có logic nghiệp vụ.
- **Application**: Orchestrator, Service. Không import thư viện hardware.
- **Domain**: Model, Interface thuần C#. Không phụ thuộc bất kỳ NuGet nào.
- **Infrastructure**: Cài đặt interface. Được phép import OpenCvSharp, SQLite.
- **HAL**: Chỉ bọc driver/SDK. Không chứa business logic.

⛔ **UI không được gọi thẳng vào Infrastructure hoặc HAL.**

✅ **Luồng đúng:** UI → Application (qua interface) → Infrastructure → HAL.

## 2. Naming Convention

| Loại             | Quy tắc                              | Ví dụ                              |
|------------------|--------------------------------------|------------------------------------|
| Class/Interface  | PascalCase. Interface bắt đầu bằng I | ICameraService, LedDetector        |
| Method/Property  | PascalCase                           | StartPreview(), FrameReady         |
| Private field    | _camelCase                           | _captureLoop, _isRunning           |
| Local variable   | camelCase                            | bitmap, ledResult                  |
| Constant         | UPPER_SNAKE_CASE                     | MAX_RETRY_COUNT                    |
| Event            | PascalCase + EventArgs suffix        | FrameReadyEventArgs                |
| Async method     | Suffix Async                         | CaptureFrameAsync()                |
| Test method      | Should_When format                   | Should_ReturnPass_When_LedOn       |

## 3. File & Folder

- Mỗi file .cs chỉ chứa 1 class/interface.
- CameraService.cs ↔ class CameraService
- Infrastructure/Camera → HardwareTest.Infrastructure.Camera
- Test project đặt trong /tests/, mirror cấu trúc src/.

```text
HardwareTestApp/
  src/
    Presentation/Forms/  · Controls/
    Application/         · Services, Orchestrators
    Domain/              · Models, Interfaces
    Infrastructure/      · Camera, Uart, DB
    HAL/                 · CaptureLoop, SerialWrapper
  tests/
    Domain.Tests/  · Application.Tests/
```

## 4. Code Style
### 4.1  Thread Safety — Camera
CaptureLoop chạy trên background Task. Mọi cập nhật UI đều phải qua Invoke:
if (control.InvokeRequired)
    control.Invoke(() => pictureBox.Image = e.Frame);

•	Không dùng Thread.Sleep trong capture loop — dùng CancellationToken.
•	FrameBuffer dùng ConcurrentQueue<Bitmap>, giới hạn tối đa 3 frame để tránh memory leak.
•	Dispose Bitmap cũ trước khi gán frame mới.
4.2  Dependency Injection
•	Constructor injection — không dùng Service Locator hoặc static factory.
•	UI form nhận interface qua constructor, không new trực tiếp Infrastructure class.
public CameraPreviewControl(ICameraService cameraService)
{
    _camera = cameraService;
}
4.3  Error Handling
•	Không dùng catch(Exception) chung chung — bắt exception cụ thể.
•	Lỗi từ hardware (UART, Camera) phải log rõ DeviceIndex / ComPort.
•	TestCase FAIL phải kèm message mô tả nguyên nhân — không trả về FAIL trống.
•	Dùng ILogger (Microsoft.Extensions.Logging) — không Console.WriteLine.
4.4  Async / Await
•	Mọi thao tác I/O (camera, UART, file) phải async.
•	Không dùng .Result hoặc .Wait() — gây deadlock trên UI thread.
•	ConfigureAwait(false) trong Infrastructure và HAL layer.
5. Test Case Convention
•	Mỗi TestCase là một class kế thừa ITestStrategy.
•	 chạy nhiều lần cho cùng kết quả.
•	Mỗi TestStep có Timeout riêng — mặc định 5000ms.
•	Kết quả trả về TestResult { Status, Message, ActualValue, ExpectedValue }.

Cấu hình test case lưu trong test-cases.json, không hardcode trong code:
{ "id": "TC_LED_01", "name": "LED Green ON",
  "steps": [{ "command": "LED_ON", "expectedLed": "GREEN",
             "timeout": 3000 }] }
6. Git Convention
Branch
•	feature/ten-tinh-nang — tính năng mới
•	fix/mo-ta-loi — bug fix
•	refactor/ten-module — refactor
Commit message
[feat] Thêm LedColorDetector với HSV threshold
[fix] Sửa InvokeRequired null ref khi form đóng
[refactor] Tách CaptureLoop ra HAL layer

•	Không commit file build (bin/, obj/, *.user).
•	.gitignore phải loại trừ appsettings.local.json (chứa COM port config).
7. Checklist trước khi merge
•	✅  Không có warning build.
•	✅  Unit test Domain.Tests và Application.Tests pass.
•	✅  Không có hardcoded string (COM port, camera index, timeout).
•	✅  Không có Console.WriteLine hay Debug.Print còn lại.
•	✅  Dispose được gọi đúng chỗ (Camera, SerialPort, Bitmap).
•	✅  TestCase mới có file JSON config tương ứng.

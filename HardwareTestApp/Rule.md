# Quy ước coding WinForms Hardware Test

**Phiên bản 1.1  |  C# .NET  |  WinForms + OpenCvSharp**

---

## 1. Kiến trúc & Layer

Project tuân theo Clean Architecture 5 layer. Mỗi layer chỉ được phép phụ thuộc vào layer bên trong nó.

| Layer | Trách nhiệm | Được phép import |
|---|---|---|
| **Presentation** | WinForms, UserControl. Không có logic nghiệp vụ | — |
| **Application** | Orchestrator, Service. Điều phối luồng nghiệp vụ | Domain interfaces |
| **Domain** | Model, Interface thuần C# | Không NuGet nào |
| **Infrastructure** | Cài đặt interface phần cứng & lưu trữ | OpenCvSharp, SQLite, System.IO.Ports |
| **HAL** | Bọc trực tiếp driver/SDK | Thư viện driver cụ thể |

⛔ **UI không được gọi thẳng vào Infrastructure hoặc HAL.**

✅ **Luồng đúng:** UI → Application (qua interface) → Infrastructure → HAL.

### Import bị cấm (vi phạm layer)

| Layer | Cấm import |
|---|---|
| Domain | Bất kỳ NuGet nào, System.IO.Ports, OpenCvSharp |
| Application | OpenCvSharp, System.IO.Ports, SQLite — mọi thư viện hardware |
| Presentation | Infrastructure class trực tiếp, HAL class trực tiếp |
| HAL | Business logic, Application/Domain namespace |

### Đăng ký DI

Toàn bộ dependency injection được wire tại một điểm duy nhất: `Program.cs` hoặc class `CompositionRoot` riêng nếu số lượng dependency lớn. Không wire DI rải rác trong từng Form hay Control.

---

## 2. Quy ước đặt tên

| Loại | Quy tắc | Ví dụ |
|---|---|---|
| Class / Interface | PascalCase. Interface bắt đầu bằng `I` | `ICameraService`, `LedDetector` |
| Method / Property | PascalCase | `StartPreview()`, `FrameReady` |
| Private field | `_camelCase` | `_captureLoop`, `_isRunning` |
| Local variable | camelCase | `bitmap`, `ledResult` |
| Constant | `UPPER_SNAKE_CASE` | `MAX_RETRY_COUNT` |
| Enum type & member | PascalCase | `enum LedColor { Red, Green, Off }` |
| Event | PascalCase + `EventArgs` suffix | `FrameReadyEventArgs` |
| Async method | Suffix `Async` | `CaptureFrameAsync()` |
| Test method | Định dạng `Should_When` | `Should_ReturnPass_When_LedOn` |

### Namespace

Pattern bắt buộc: `FCT.G6T.{Layer}.{SubFolder}`

Ví dụ: `FCT.G6T.Infrastructure.Camera`, `FCT.G6T.Application`, `FCT.G6T.Domain.Models`

---

## 3. File & Thư mục

- Mỗi file `.cs` chỉ chứa 1 class hoặc interface.
- Tên file phải khớp chính xác tên class: `CameraService.cs` ↔ `class CameraService`.
- Test project đặt trong `/tests/`, mirror cấu trúc `src/`.
- File config JSON đặt tại `config/`, đặt tên theo pattern: `{device-type}-test-cases.json` (kebab-case).
  - Ví dụ: `smoke-test-cases.json`, `heat-test-cases.json`

```text
FCT-G6T/
  src/
    Presentation/Forms/  · Controls/
    Application/         · Services, Orchestrators
    Domain/              · Models, Interfaces
    Infrastructure/      · Camera, Serial, DB
    HAL/                 · CaptureLoop, SerialWrapper
  config/
    smoke-test-cases.json
    heat-test-cases.json
  tests/
    Domain.Tests/  · Application.Tests/
```

---

## 4. Phong cách code

### 4.1 An toàn luồng — Camera

`CaptureLoop` chạy trên background `Task`. Mọi cập nhật UI phải qua `Invoke`:

```csharp
// Pattern chuẩn — luôn xử lý cả 2 nhánh
if (control.InvokeRequired)
    control.Invoke(() => pictureBox.Image = e.Frame);
else
    pictureBox.Image = e.Frame;
```

- Không dùng `Thread.Sleep` trong capture loop — dùng `CancellationToken`.
- `FrameBuffer` dùng `ConcurrentQueue<Bitmap>`, giới hạn tối đa **3 frame** để tránh memory leak.
- `Dispose` `Bitmap` cũ **trước** khi gán frame mới.

**Vòng đời CaptureLoop:**

| Sự kiện | Hành động |
|---|---|
| `Form.Load` | Khởi tạo `CancellationTokenSource`, gọi `CaptureLoop.StartAsync(token)` |
| `Form.FormClosing` | Gọi `_cts.Cancel()`, `await` task kết thúc trước khi đóng form |

**Đồng thời hóa TestOrchestrator:** Mỗi lần chỉ được chạy 1 test run. Trong khi đang test, button `[START TEST]` phải bị disable. Không cho phép chạy song song 2 test run.

### 4.2 Tiêm phụ thuộc

- Constructor injection — **không** dùng Service Locator hoặc static factory.
- UI Form nhận interface qua constructor, không `new` trực tiếp Infrastructure class.

```csharp
public CameraPreviewControl(ICameraService cameraService)
{
    _camera = cameraService;
}
```

### 4.3 Xử lý lỗi

**Phân loại exception:**

| Loại | Ví dụ | Cách xử lý |
|---|---|---|
| **Recoverable** | UART timeout, LED detect miss, read response fail | `TestOrchestrator` catch, FAIL step, ghi log, tiếp tục teardown |
| **Fatal** | Camera disconnect giữa chừng, SerialPort đóng đột ngột | Throw custom exception lên Application layer, abort toàn bộ test run, báo UI |
| **System** | `OutOfMemoryException`, `StackOverflowException` | Không catch — để escalate tự nhiên |

- Không dùng `catch(Exception)` chung chung — bắt exception cụ thể.
- Lỗi hardware (UART, Camera) phải log rõ `DeviceIndex` / `ComPort`.
- `TestCase` FAIL phải kèm `Message` mô tả nguyên nhân — không trả về FAIL trống.
- Dùng `ILogger` (Microsoft.Extensions.Logging) — **cấm** `Console.WriteLine` và `Debug.Print`.

**Mức log:**

| Mức | Khi nào dùng |
|---|---|
| `LogInformation` | Flow bình thường: bắt đầu step, kết quả PASS, kết nối thành công |
| `LogWarning` | Timeout lần đầu, retry, giá trị ngoài ngưỡng nhưng chưa fail |
| `LogError` | Hardware error, test FAIL, exception recoverable |

**File log:** Xuất ra `logs/fct-{yyyy-MM-dd}.log`, rolling theo ngày, giữ tối đa **30 ngày**.

### 4.4 Async/Await

- Mọi thao tác I/O (camera, UART, file) phải `async`.
- **Không** dùng `.Result` hoặc `.Wait()` — gây deadlock trên UI thread.
- `ConfigureAwait(false)` trong `Infrastructure` và `HAL` layer.
- **`CancellationToken` propagation:** UI tạo `CancellationTokenSource`, truyền token qua `TestOrchestrator` → Adapter → HAL. Không tạo token mới ở layer dưới.

**`async void` trong WinForms event handler:**

`async void` là bắt buộc cho event handler nhưng exception sẽ không bị bắt nếu để thoát ra ngoài. Mọi `async void` handler **phải** bọc toàn bộ thân trong `try/catch`:

```csharp
// ✅ Đúng — exception được kiểm soát
private async void BtnStartTest_Click(object sender, EventArgs e)
{
    try
    {
        btnStartTest.Enabled = false;
        await _orchestrator.RunAsync(_cts.Token);
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("Test run cancelled by user.");
    }
    catch (HardwareException ex)
    {
        _logger.LogError(ex, "Hardware error during test run.");
        MessageBox.Show($"Lỗi phần cứng: {ex.Message}");
    }
    finally
    {
        btnStartTest.Enabled = true;
    }
}

// ❌ Sai — exception thoát ra, app crash không báo lỗi
private async void BtnStartTest_Click(object sender, EventArgs e)
{
    await _orchestrator.RunAsync(_cts.Token);
}
```

---

## 5. Quy ước Test Case

- Mỗi `TestCase` là một class kế thừa `ITestStrategy`.
- Test case phải **idempotent** — chạy nhiều lần liên tiếp cho cùng kết quả.
- Mỗi `TestStep` có `Timeout` riêng — mặc định **5000 ms**.
- Kết quả trả về `TestResult { Status, Message, ActualValue, ExpectedValue }`.

### Quy ước ID

Schema bắt buộc: `TC_{DEVICE}_{SEQ:02d}`

- `DEVICE`: `SMOKE` | `HEAT` | `BELL` | `BUTTON`
- `SEQ`: số thứ tự 2 chữ số, bắt đầu từ `01`
- Ví dụ: `TC_SMOKE_01`, `TC_BELL_03`

### Teardown (bắt buộc)

Mỗi `TestCase` **phải** định nghĩa `teardown` steps — chạy dù PASS hay FAIL, tương đương `finally`. Mục đích: đảm bảo GPIO reset về trạng thái an toàn, không để tín hiệu treo.

```json
{
  "id": "TC_SMOKE_01",
  "name": "Đầu báo khói — kiểm tra LED và phản hồi",
  "deviceType": "SMOKE",
  "steps": [
    {
      "order": 1,
      "description": "Kích GPIO kênh 1 — mô phỏng báo động",
      "command": "GPIO_SET 1 HIGH",
      "expectedLed": "RED",
      "timeout": 5000,
      "maxRetry": 0
    }
  ],
  "teardown": [
    {
      "order": 1,
      "description": "Reset GPIO về trạng thái an toàn",
      "command": "GPIO_SET 1 LOW",
      "timeout": 3000
    }
  ]
}
```

### Thử lại

Mỗi `TestStep` có field `maxRetry` (mặc định `0`). Nếu `0`: fail ngay khi timeout. Nếu `> 0`: thử lại tối đa `maxRetry` lần trước khi FAIL.

---

## 6. Quy ước Git

### Nhánh

Branch **luôn** rẽ từ `develop`, merge về `develop`. `main` chỉ nhận release merge.

```
main       ← chỉ cho phát hành
develop    ← nhánh tích hợp
  └── feature/ten-tinh-nang
  └── fix/mo-ta-loi
  └── refactor/ten-module
```

### Thông điệp commit

```
[feat] Thêm LedColorDetector với HSV threshold
[fix] Sửa InvokeRequired null ref khi form đóng
[refactor] Tách CaptureLoop ra HAL layer
```

### Chiến lược merge

- **Feature → develop**: Squash merge (giữ history gọn).
- **Develop → main** (phát hành): Merge commit (giữ traceability).

### Khác

- Không commit file build (`bin/`, `obj/`, `*.user`).
- `.gitignore` phải loại trừ `appsettings.local.json` (chứa COM port config).

---

## 7. Checklist trước khi merge

- ✅ Không có warning build.
- ✅ Unit test `Domain.Tests` và `Application.Tests` pass.
- ✅ Không có hardcoded string (COM port, camera index, timeout).
- ✅ Không có `Console.WriteLine` hay `Debug.Print` còn lại.
- ✅ `Dispose` được gọi đúng chỗ (`Camera`, `SerialPort`, `Bitmap`).
- ✅ `TestCase` mới có file JSON config tương ứng với đủ `teardown` steps.
- ✅ Mọi `async void` event handler có `try/catch` bao toàn thân.
- ✅ Ít nhất 1 approver review trước khi merge.
- ✅ PR liên quan Camera/Serial: chạy ít nhất 50 lần test liên tiếp, xác nhận memory không tăng liên tục.

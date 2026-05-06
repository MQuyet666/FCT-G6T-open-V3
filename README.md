# FCT-G6T — Kiểm tra mạch chức năng (Tự động kiểm tra thiết bị báo cháy)

> Phần mềm kiểm tra chức năng thiết bị báo cháy tự động dành cho công nhân vận hành dây chuyền sản xuất.

**Ứng dụng kiểm thử tự động cho thiết bị báo cháy (đầu báo khói, đầu báo nhiệt, chuông đèn, nút bấm), kiểm tra LED theo thời gian thực qua USB camera và điều khiển phần cứng qua UART.**

---

## 📋 Mục tiêu

FCT-G6T giúp công nhân kiểm tra nhanh các thiết bị phòng cháy chữa cháy trước khi xuất xưởng. Mỗi thiết bị trải qua bộ bài test tự động, kết quả **PASS / FAIL** rõ ràng mà không yêu cầu kiến thức kỹ thuật sâu.

---

## ✨ Tính năng chính

| Tính năng | Mô tả |
|---|---|
| **Chọn loại thiết bị** | Đầu báo khói · Đầu báo nhiệt · Chuông đèn · Nút bấm |
| **Cấu hình cổng COM** | Thiết lập riêng cho G6T board và DUT với baud rate tùy chỉnh |
| **Kiểm tra tự động** | Thực thi từng test step theo kịch bản JSON, hiển thị tiến độ theo thời gian thực |
| **Phát hiện LED bằng camera** | OpenCV phân tích khung hình từ USB camera (1280×720 @ 30 FPS) để nhận diện LED (HSV color matching) |
| **Kết quả PASS / FAIL** | Hiển thị trực quan, ghi log chi tiết với timestamp, hex dump UART traffic |
| **Điều khiển GPIO** | PC ↔ G6T board giao tiếp UART, điều khiển relay, đọc tín hiệu GPIO |
| **Ghi log tự động** | Log rolling theo ngày (`logs/fct-{yyyy-MM-dd}.log`), lưu 30 ngày |

---

## 🏗️ Kiến trúc hệ thống

### **Sơ đồ vật lý**

```
┌──────────────────────────────────────────────────────┐
│            PC (Ứng dụng WinForms FCT-G6T)            │
│                                                      │
│  ┌─────────────────────────────────────────────┐    │
│  │   Layer Presentation (UI WinForms)          │    │
│  │   • Mainform (chọn thiết bị, nút điều khiển) │    │
│  │   • CameraPreviewControl (preview camera)   │    │
│  └──────────────────┬──────────────────────────┘    │
│                     │                                │
│  ┌──────────────────▼──────────────────────────┐    │
│  │   Layer Application                          │    │
│  │   • TestOrchestrator                        │    │
│  │   • SmokeDeviceTestService                  │    │
│  │   • CameraPreviewAppService                 │    │
│  └──────────────────┬──────────────────────────┘    │
│                     │                                │
│  ┌──────────────────▼──────────────────────────┐    │
│  │   Layer Infrastructure                       │    │
│  │   • G6TAdapter (điều khiển UART)            │    │
│  │   • DetectorAdapter (đọc dữ liệu UART)      │    │
│  │   • SdkCameraAdapter (SDK DVPCamera)        │    │
│  │   • JsonTestCaseProvider (tải cấu hình)     │    │
│  │   • FileLogger (log rolling)                │    │
│  └──────────────────┬──────────────────────────┘    │
│                     │                                │
│  ┌──────────────────▼──────────────────────────┐    │
│  │   Layer HAL (Trừu tượng phần cứng)          │    │
│  │   • SerialPortWrapper (System.IO.Ports)     │    │
│  │   • DVPCamera SDK (DLL native)              │    │
│  └─────────────────────────────────────────────┘    │
└───────────┬──────────────────┬──────────────────────┘
            │                  │
            │                  │
    ┌───────▼──────┐    ┌──────▼────────┐
    │  UART/COM3   │    │  UART/COM4    │
    │   9600 Baud  │    │  9600 Baud    │
    └───────┬──────┘    └──────┬────────┘
            │                  │
    ┌───────▼──────────────────▼─────┐
    │   Giao tiếp UART kép           │
    │   Xác thực frame + CRC         │
    │   Xử lý ACK/timeout            │
    └────────┬──────────────┬────────┘
             │              │
      ┌──────▼──┐      ┌────▼──────┐
      │ G6T     │      │   DUT     │
      │ Board   │      │  Thiết bị │
      │ (GPIO   │      │  (Đọc     │
      │ Control)│      │  cảm biến)│
      └─────────┘      └───────────┘

   Camera: Thiết bị USB Video (1280×720 @ 30 FPS)
   Phát hiện LED: OpenCV + HSV color matching
```

### **Clean Architecture 5 lớp**

```
┌─────────────────────────────────────────────────────┐
│ 5. PRESENTATION                                     │
│    (WinForms UI, thành phần hiển thị cho người dùng)│
├─────────────────────────────────────────────────────┤
│ 4. APPLICATION                                      │
│    (Điều phối nghiệp vụ, dịch vụ test)              │
├─────────────────────────────────────────────────────┤
│ 3. DOMAIN                                           │
│    (Logic thuần, model, interface — không phụ thuộc)│
├─────────────────────────────────────────────────────┤
│ 2. INFRASTRUCTURE                                   │
│    (Thích nghi interface Domain với dịch vụ ngoài)  │
├─────────────────────────────────────────────────────┤
│ 1. HAL (Hardware Abstraction Layer)                 │
│    (Wrapper mỏng cho API phần cứng/OS)              │
└─────────────────────────────────────────────────────┘

Quy tắc chính:
✅ UI gọi Application (qua interface)
✅ Application gọi Domain & Infrastructure
✅ Infrastructure thích nghi interface Domain
✅ Không phụ thuộc ngược
⛔ UI không gọi trực tiếp Infrastructure/HAL
✅ Constructor injection, không dùng service locator
✅ Async/await xuyên suốt (không .Wait()/.Result)
```

---

## 📦 Các thiết bị được hỗ trợ

| Thiết bị | Mã | Bài test |
|---|---|---|
| Đầu báo khói | `SMOKE` | Kích thử báo động, kiểm tra LED (Đỏ), đọc tín hiệu phản hồi |
| Đầu báo nhiệt | `HEAT` | Kích thử báo động, kiểm tra LED (Vàng), đọc nhiệt độ |
| Chuông đèn | `BELL` | Kích chuông (LoRa), phát hiện đèn nhấp nháy, đo thời gian phản hồi |
| Nút bấm | `BUTTON` | Nhấn GPIO, kiểm tra tín hiệu trả về, LED xác nhận |

---

## 🗂️ Cấu trúc thư mục / Project Structure

```
HardwareTestApp/
├── Program.cs                      # Điểm vào, cấu hình DI container
├── README.md                       # Tài liệu này
├── Rule.md                         # Quy ước coding & kiến trúc
├── HardwareTestApp.csproj         # File project (net7.0-windows, WinForms)
│
├── src/                            # Mã nguồn (kiến trúc 5 layer)
│   ├── Presentation/               # Layer 5: UI WinForms
│   │   ├── Forms/
│   │   │   ├── Mainform.cs        # Cửa sổ chính (chọn thiết bị, điều khiển test)
│   │   │   ├── Mainform.Designer.cs
│   │   │   └── Mainform.resx      # Tài nguyên nhúng
│   │   └── Controls/
│   │       └── CameraPreviewControl.cs  # Control preview camera live
│   │
│   ├── Application/                # Layer 4: Dịch vụ điều phối nghiệp vụ
│   │   ├── Services/
│   │   │   ├── TestOrchestrator.cs      # Điều phối lệnh G6T
│   │   │   ├── SmokeDeviceTestService.cs # Engine chạy test chính
│   │   │   └── CameraPreviewAppService.cs # Wrapper camera an toàn cho UI
│   │   └── Interfaces/
│   │       ├── ITestCaseProvider.cs
│   │       ├── ICameraPreviewAppService.cs
│   │       ├── ISmokeDeviceTestService.cs
│   │       └── IComPortProvider.cs
│   │
│   ├── Domain/                     # Layer 3: Logic domain thuần (không phụ thuộc)
│   │   ├── Models/
│   │   │   ├── TestCase.cs, TestStep.cs, TestStepResult.cs
│   │   │   ├── G6TCommand.cs, G6TResponse.cs, DetectorResponse.cs
│   │   │   ├── CameraConfig.cs, FrameReadyEventArgs.cs
│   │   │   └── Enums: G6TCommandId, G6TPowerState, G6TCalibPinState, G6TStatus
│   │   └── Interfaces/
│   │       ├── ICameraService.cs
│   │       ├── IG6TAdapter.cs
│   │       └── IDetectorAdapter.cs
│   │
│   ├── Infrastructure/             # Layer 2: Triển khai dịch vụ ngoài
│   │   ├── Serial/
│   │   │   ├── G6TAdapter.cs       # UART giao tiếp G6T board
│   │   │   ├── DetectorAdapter.cs  # UART giao tiếp thiết bị DUT
│   │   │   └── ComPortProvider.cs  # Liệt kê cổng COM
│   │   ├── Camera/
│   │   │   ├── SdkCameraAdapter.cs # Tích hợp DVPCamera SDK
│   │   │   └── OpenCvCameraAdapter.cs # Triển khai OpenCV thay thế
│   │   ├── Configuration/
│   │   │   └── JsonTestCaseProvider.cs # Tải test case từ file JSON
│   │   └── Logging/
│   │       ├── FileLogger.cs
│   │       └── FileLoggerProvider.cs  # ILoggerProvider tuỳ biến
│   │
│   └── HAL/                        # Layer 1: Hardware Abstraction Layer
│       ├── SerialPortWrapper.cs    # Wrapper cho System.IO.Ports.SerialPort
│       ├── ISerialPortWrapper.cs   # Interface trừu tượng
│       └── sdk1/                   # Thư viện DVPCamera SDK
│           ├── DVPCameraCS64.dll   # Wrapper C# managed
│           ├── DVPCamera64.dll     # Driver camera native
│           └── DVPCameraTL64.cti   # Transport layer camera
│
├── config/                         # Cấu hình & kịch bản test
│   ├── appsettings.json           # Thiết lập runtime (timeout, baud rate, ...)
│   ├── camera.json                # Index camera & độ phân giải
│   ├── smoke-test-cases.json      # Kịch bản test đầu báo khói
│   ├── heat-test-cases.json       # Kịch bản test đầu báo nhiệt
│   ├── bell-test-cases.json       # Kịch bản test chuông đèn
│   └── button-test-cases.json     # Kịch bản test nút bấm
│
├── logs/                           # Log tự động theo ngày
│   ├── fct-2026-04-28.log
│   └── fct-2026-04-27.log
│
├── bin/Debug & bin/Release/       # Output biên dịch
└── obj/                            # Tệp build
```

---

## 🛠️ Công nghệ & phụ thuộc

### **.NET & Framework cốt lõi**

| Gói | Phiên bản | Mục đích |
|---------|---------|---------|
| **.NET** | 7.0-windows | Runtime ứng dụng desktop |
| **WinForms** | (implicit trong 7.0-windows) | Framework UI |
| **System.IO.Ports** | 8.0.0 | Giao tiếp cổng serial (UART) |

### **Dependency Injection & Configuration**

| Gói | Phiên bản | Mục đích |
|---------|---------|---------|
| **Microsoft.Extensions.DependencyInjection** | 8.0.0 | DI container (đăng ký dịch vụ) |
| **Microsoft.Extensions.Configuration** | 8.0.0 | Quản lý cấu hình |
| **Microsoft.Extensions.Configuration.Json** | 8.0.0 | Binding file cấu hình JSON |
| **Microsoft.Extensions.Configuration.Binder** | 8.0.0 | Deserialize cấu hình type-safe |
| **Microsoft.Extensions.Logging** | 8.0.0 | Trừu tượng logging |
| **Microsoft.Extensions.Logging.Abstractions** | 8.0.0 | Interface logger |

### **Xử lý ảnh & thị giác máy tính**

| Gói | Phiên bản | Mục đích |
|---------|---------|---------|
| **OpenCvSharp4** | 4.13.0.20260302 | Binding OpenCV (phát hiện LED theo HSV) |
| **OpenCvSharp4.runtime.win** | 4.13.0.20260302 | Thư viện OpenCV native (Windows x64) |

### **SDK phần cứng**

| Gói | Loại | Mục đích |
|---------|------|---------|
| **DVPCameraCS64** | DLL cục bộ trong `src/HAL/sdk1/` | SDK camera DVP Hikvision (độc quyền) |
| **System.IO.Ports** | Tích hợp sẵn trong .NET | Giao tiếp cổng serial cho UART |

---

## ⚙️ Tệp cấu hình

### **1. `appsettings.json`** — Thiết lập runtime

```json
{
  "Serial": {
    "G6tBaudRate": 9600,              // Baud rate của G6T board
    "DetectorBaudRate": 9600,         // Baud rate của thiết bị DUT
    "ReadTimeoutMs": 100,             // Timeout đọc serial (ms)
    "WriteTimeoutMs": 1000,           // Timeout ghi serial (ms)
    "FrameRetryCount": 1,             // Số lần retry frame G6T
    "DetectorRetryCount": 1,          // Số lần retry frame DUT
    "FrameAckTimeoutSeconds": 3,      // Timeout ACK G6T (giây)
    "DetectorAckTimeoutSeconds": 3    // Timeout ACK DUT (giây)
  },
  "TestTimeouts": {
    "CommandAckTimeoutSeconds": 3,    // Thời gian chờ ACK lệnh G6T
    "LedDetectTimeoutSeconds": 5,     // Thời gian chờ phát hiện LED (từ camera)
    "ButtonTestTimeoutSeconds": 15,   // Timeout phản hồi test nút bấm
    "LedDetectPollDelayMs": 100       // Chu kỳ poll phát hiện LED
  },
  "CameraRuntime": {
    "FrameBufferSize": 3,             // Số frame tối đa trong buffer
    "CameraRetryIntervalSeconds": 2   // Khoảng thời gian retry nếu camera lỗi
  },
  "Logging": {
    "Directory": "logs",              // Thư mục log
    "FilePrefix": "fct-",             // Tiền tố tên file log
    "RetentionDays": 30               // Tự xóa log quá 30 ngày
  }
}
```

### **2. `camera.json`** — Cấu hình camera

```json
{
  "DeviceIndex": 0,      // Index camera USB (0 = camera đầu tiên)
  "Width": 1280,         // Chiều rộng frame (pixel)
  "Height": 720,         // Chiều cao frame (pixel)
  "TargetFps": 30        // FPS mục tiêu
}
```

### **3. File Test Case** — Kịch bản theo từng thiết bị

Ví dụ: `smoke-test-cases.json`

```json
[
  {
    "id": "TC_SMOKE_01",
    "name": "Kiểm tra LED khi bật nguồn",
    "deviceType": "SMOKE",
    "steps": [
      {
        "order": 1,
        "description": "Bật nguồn và chờ LED đỏ",
        "command": "POWER_ON",
        "expectedLed": "RED",
        "timeout": 5000,
        "maxRetry": 0
      },
      {
        "order": 2,
        "description": "Test buzzer nút bấm (thiết bị khói DUT)",
        "command": "TEST_BUTTON_BUZZER",
        "expectedLed": "",
        "timeout": 15000,
        "maxRetry": 1
      }
    ],
    "teardown": [
      {
        "order": 1,
        "description": "Tắt nguồn thiết bị",
        "command": "POWER_OFF",
        "expectedLed": "",
        "timeout": 3000,
        "maxRetry": 0
      }
    ]
  }
]
```

**Cấu trúc:**

- **`steps`** — Chuỗi test chính (thực thi theo thứ tự)
- **`teardown`** — Bước dọn dẹp (luôn chạy kể cả khi fail)
- **`expectedLed`** — Màu LED kỳ vọng (RED, YELLOW, GREEN, hoặc rỗng)
- **`timeout`** — Thời gian chờ hoàn thành step (ms)
- **`maxRetry`** — Số lần thử lại nếu step fail

---

## 🚀 Xây dựng & chạy

### **Yêu cầu**

- **Windows 10/11** (x64)
- **.NET SDK 7.0** hoặc mới hơn
- **Visual Studio 2022** (hoặc VS Code + C# Dev Kit)
- **USB Camera** (tương thích DVPCamera SDK, thường là HikVision)
- **Phần cứng:** G6T board, thiết bị DUT, 2× USB-to-UART (cho cổng COM)

### **Build**

```powershell
# Restore gói NuGet
dotnet restore

# Build ở chế độ Debug
dotnet build --configuration Debug

# Build ở chế độ Release (tối ưu)
dotnet build --configuration Release
```

### **Run**

```powershell
# Chạy trực tiếp
dotnet run --project HardwareTestApp.csproj

# Hoặc chạy file thực thi đã build
./bin/Debug/net7.0-windows/HardwareTestApp.exe
```

### **Build & Run từ VS Code**

Nhấn `Ctrl+Shift+B` để xem task build, hoặc `F5` để debug (cần cấu hình launch).

---

## 🧪 Cách sử dụng

### **Quy trình test từng bước**

1. **Chuẩn bị phần cứng**
   - Kết nối G6T board với PC qua USB-UART (ghi lại COM, ví dụ COM3)
   - Kết nối thiết bị DUT qua USB-UART riêng (ghi lại COM, ví dụ COM4)
   - Gắn USB camera hướng vào thiết bị cần test
   - Căn chỉnh camera để LED nằm trong ROI
   - Cấp nguồn cho phần cứng (G6T board và DUT)

2. **Khởi động ứng dụng**

   ```powershell
   dotnet run --project HardwareTestApp.csproj
   ```

3. **Cấu hình cổng COM**
   - **Cổng COM G6T:** Chọn từ dropdown (ví dụ COM3)
   - **Cổng COM DUT:** Chọn từ dropdown (ví dụ COM4)

4. **Chọn loại thiết bị**
   - Radio buttons: Đầu báo khói, Đầu báo nhiệt, Chuông đèn, Nút bấm
   - Ví dụ: Chọn "Đầu báo khói" để test cảm biến khói

5. **Điều chỉnh vùng ROI camera (tùy chọn)**
   - Preview camera hiển thị trong control
   - Click + kéo để di chuyển hình chữ nhật ROI
   - Viền ROI chuyển **XANH** khi phát hiện LED, **ĐỎ** khi không thấy LED

6. **Nhấn nút START**
   - Test chạy tự động: bật nguồn → bắt LED → test nút → tắt nguồn
   - Log tiến độ cập nhật theo thời gian thực (RTB textbox)
   - Đèn trạng thái: Đỏ (OFF), Xanh (ON)

7. **Xem kết quả**
   - **PASS** → Tất cả step đạt, thiết bị đạt
   - **FAIL** → Có step lỗi, thiết bị bị loại kèm lý do trong log
   - Kiểm tra `logs/fct-{yyyy-MM-dd}.log` để chẩn đoán chi tiết

8. **Lặp lại hoặc dừng**
   - Nhấn START để test thiết bị tiếp theo
   - Đóng ứng dụng để dừng (log tự lưu)

---

## 🔧 Hướng dẫn phát triển

### **Quy ước code (theo `Rule.md`)**

✅ **Clean Architecture 5 lớp**

- Phân tách nghiêm ngặt: UI → Application → Domain ← Infrastructure ← HAL
- Không phụ thuộc ngược

✅ **Dependency Injection**

- Mọi service đăng ký trong DI container `Program.cs`
- Không dùng `new` trong production code (trừ factory)
- Bắt buộc constructor injection

✅ **Async/Await**

- Mọi thao tác I/O đều async: `Task`, `Task<T>`
- **Cấm:** `.Wait()`, `.Result`, `.GetAwaiter().GetResult()`
- Dùng `await` ở mọi tầng

✅ **Cập nhật UI Camera**

- Mọi cập nhật frame đều kiểm tra `Control.InvokeRequired`
- Không cập nhật UI trực tiếp từ background thread
- Dùng pattern `this.Invoke(delegate { /* cập nhật UI */ })`

✅ **Đồng thời hóa test**

- Chỉ **1 test chạy tại một thời điểm**
- Nút START bị disable khi đang test
- Tránh race condition trong giao tiếp UART

✅ **Cấu hình phần cứng**

- **Không hardcode COM port** — luôn đọc từ config/dropdown
- **Không hardcode camera index** — đọc từ `camera.json`
- **Không hardcode timeout** — luôn lấy từ `appsettings.json`

### **Thêm loại thiết bị mới**

1. **Tạo test case JSON** (ví dụ `config/my-device-test-cases.json`)

   ```json
   [
     {
       "id": "TC_MYDEV_01",
       "name": "Test thiết bị của tôi",
       "deviceType": "MYDEVICE",
       "steps": [ ... ],
       "teardown": [ ... ]
     }
   ]
   ```

2. **Thêm vào Application Service** (ví dụ `SmokeDeviceTestService.cs`)
   - Thêm case trong switch cho `deviceType` mới
   - Dùng lại `TestOrchestrator` cho lệnh G6T
   - Dùng lại `DetectorAdapter` để đọc DUT

3. **Cập nhật UI** (`Mainform.cs`)
   - Thêm radio button hoặc option dropdown cho loại thiết bị mới
   - Bind lựa chọn vào biến `deviceType`

4. **Test kỹ**
   - Kiểm tra log UART (`logs/fct-*.log`)
   - Kiểm tra phát hiện LED qua preview camera
   - Test các kịch bản timeout & retry

### **Cách thêm lệnh G6T mới**

1. **Thêm vào enum** (`Domain/Models/G6TCommandId.cs`)

   ```csharp
   public enum G6TCommandId
   {
       PowerControl = 0x01,
       TestButton = 0x02,
       MyNewCommand = 0x0A  // ← Thêm ở đây
   }
   ```

2. **Thêm vào `TestOrchestrator`** (`Application/Services/TestOrchestrator.cs`)

   ```csharp
   public async Task<G6TResponse> MyNewCommandAsync()
   {
       var command = new G6TCommand 
       { 
           CommandId = G6TCommandId.MyNewCommand,
           Data = new byte[] { /* dữ liệu */ }
       };
       return await _g6tAdapter.SendCommandAsync(command, cancellationToken);
   }
   ```

3. **Gọi từ Service**

   ```csharp
   var response = await _testOrchestrator.MyNewCommandAsync();
   ```

### **Thực hành logging tốt**

```csharp
using Microsoft.Extensions.Logging;

public class MyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }
    
    public async Task DoSomethingAsync()
    {
        _logger.LogInformation("Bắt đầu thao tác");
        try
        {
            // ... thực hiện xử lý
            _logger.LogInformation("Hoàn tất thao tác");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Thao tác thất bại");
            throw;
        }
    }
}
```

Log xuất ra `logs/fct-{yyyy-MM-dd}.log` (rolling theo ngày).

---

## 🔌 Cấu hình phần cứng

### **Giao thức UART**

**G6T board & DUT** dùng giao thức UART dạng frame tùy biến:

```
Định dạng frame:
┌─────────┬─────────┬────────┬──────────┬─────┐
│ 0x1F    │ 0x2F    │ 0x3F   │ 0xFF     │ Lệnh│
│ (tiền tố, 4 byte)                     │ ID  │
└─────────┴─────────┴────────┴──────────┴─────┤
  ↓
┌────────┬──────────┬─────────┬────────┐
│ Độ dài │ Dữ liệu… │ CRC16   │ ACK    │
│ (1B)   │ (NB)     │ (2B)    │ (Tuỳ)  │
└────────┴──────────┴─────────┴────────┘

Baud Rate: 9600 (mặc định, có thể cấu hình)
Parity: None
Stop Bits: 1
Data Bits: 8
Timeout: 3 giây (có thể cấu hình)
```

### **Lệnh G6T Board**

| Lệnh | Mã | Dữ liệu | Mục đích |
|---------|------|------|---------|
| **PowerControl** | 0x01 | `{On/Off}` | Bật/tắt nguồn DUT |
| **TestButton** | 0x02 | `{}` | Test buzzer/relay |
| **SetCalibPin** | 0x03 | `{Set/Unset}` | Điều khiển chân calib |
| **TestButton2** | 0x04 | `{}` | Test GPIO pin 2 |
| **TestButton3** | 0x05 | `{}` | Test GPIO pin 3 |

### **Đọc dữ liệu cảm biến DUT**

| Dữ liệu | Timeout | Kiểu phản hồi |
|---------|---------|---|
| **Nhiệt độ** | 3 giây | `DetectorResponse { Temperature: float }` |
| **Mức khói** | 3 giây | `DetectorResponse { SmokeLevel: int }` |
| **Giá trị LoRa** | 3 giây | `DetectorResponse { LoraValue: int }` |

---

## 🎥 Camera & phát hiện LED

### **Phần cứng**

- **Camera:** USB video device (tương thích DVPCamera SDK HikVision)
- **Độ phân giải:** 1280×720 (cấu hình trong `camera.json`)
- **FPS:** 30 (có thể cấu hình)
- **Lắp đặt:** Cố định, LED ở trung tâm khung hình

### **Thuật toán phát hiện LED**

**Phát hiện theo màu HSV:**

1. **Chụp frame** từ camera (định dạng BGR24)
2. **Chuyển sang HSV**
3. **Định nghĩa dải màu** cho LED kỳ vọng (ví dụ ĐỎ: H 0-10 hoặc 170-180, S 100-255, V 100-255)
4. **Mask frame** theo dải màu → ảnh nhị phân
5. **Đếm pixel trắng** trong ROI
6. **Kết quả:** Nếu số pixel > ngưỡng → LED ON (PASS), ngược lại LED OFF (FAIL)

**Màu LED kỳ vọng:**

- **SMOKE:** LED đỏ (H: 0-10°, S: 100-255, V: 100-255)
- **HEAT:** LED vàng (H: 20-30°, S: 100-255, V: 100-255)
- **BELL:** LED xanh/vàng nhấp nháy (phát hiện chuyển động)
- **BUTTON:** LED xanh xác nhận

### **Điều chỉnh vùng ROI**

Người dùng có thể click + kéo trên preview để di chuyển ROI:

- **Mặc định:** Trung tâm frame, 100×100 pixel
- **Điều chỉnh:** Click + kéo để di chuyển hình chữ nhật
- **Phản hồi trực quan:**
  - **Viền XANH** khi phát hiện LED trong ROI
  - **Viền ĐỎ** khi không thấy LED trong ROI

---

## 📊 Ghi log & chẩn đoán

### **Vị trí file log**

```
logs/
├── fct-2026-04-28.log  (hôm nay)
├── fct-2026-04-27.log  (hôm qua)
└── ...
```

Log rolling theo ngày, tự xóa sau 30 ngày (cấu hình trong `appsettings.json`).

### **Mức log & ví dụ**

| Mức | Ví dụ | Khi nào |
|-------|---------|------|
| **Information** | `[2026-04-28 10:45:23.123] INFO: Bắt đầu chuỗi test cho thiết bị SMOKE` | Luồng bình thường |
| **Warning** | `[2026-04-28 10:46:15.456] WARN: Timeout phát hiện LED (lần 2/3), đang retry...` | Retry, timeout |
| **Error** | `[2026-04-28 10:47:02.789] ERROR: Không tìm thấy camera (DeviceIndex=0), dừng test` | Lỗi, exception |

### **Ví dụ hex dump**

```
[2026-04-28 10:45:30.123] INFO: G6T TX: 1F 2F 3F FF 01 01 01 AA BB
[2026-04-28 10:45:30.456] INFO: G6T RX: 1F 2F 3F FF 01 01 01 00 CC DD
```

(TX = gửi đi, RX = nhận về)

---

## 🐛 Giải quyết vấn đề

| Vấn đề | Nguyên nhân | Cách xử lý |
|-------|-------|----------|
| **"COM port không tìm thấy"** | USB adapter bị ngắt hoặc chọn sai cổng | Cắm lại adapter, rescan COM, chọn đúng cổng |
| **"Timeout phát hiện LED"** | Camera không bắt frame hoặc LED không rõ | Kiểm tra camera, ánh sáng, điều chỉnh ROI |
| **"Timeout ACK G6T"** | G6T không phản hồi, sai baud rate | Power cycle G6T, kiểm tra baud rate trong `appsettings.json` |
| **"Khởi tạo SDK camera thất bại"** | Thiếu DLL DVPCamera trong `src/HAL/sdk1/` | Kiểm tra DLL, cài lại driver camera |
| **"Test bị treo"** | Nút START vẫn disable, test trước chưa kết thúc | Chờ test xong, kiểm tra log xem step bị kẹt |
| **Log không được tạo** | Thiếu thư mục log hoặc lỗi quyền | Kiểm tra thư mục `logs/`, quyền ghi |

**Gợi ý debug:**

1. Mở `logs/fct-*.log` trong/sau test để xem hex dump chi tiết
2. Bật log chi tiết: kiểm tra log level trong `appsettings.json` (nếu có)
3. Kiểm tra phần cứng: test UART bằng công cụ serial monitor
4. Kiểm tra camera riêng: dùng phần mềm của hãng để xác nhận hoạt động

---

## 📝 Quy tắc & hội ước

Xem [Rule.md](Rule.md) để biết chuẩn coding chi tiết:

- Clean Architecture 5 layer
- Bắt buộc constructor injection
- Không hardcode giá trị (camera index, timeout, COM port)
- Async/await xuyên suốt
- Cập nhật UI qua `Invoke()`
- Logging đầy đủ

---

## 📄 Giấy phép & Đóng góp

**Giấy phép:** [Ghi rõ giấy phép, ví dụ MIT, Proprietary, ...]

**Đóng góp:** Vui lòng tuân thủ quy ước trong `Rule.md` trước khi gửi PR.

---

## 📧 Hỗ trợ

Nếu có lỗi, câu hỏi hoặc yêu cầu tính năng:

1. Kiểm tra `logs/fct-*.log` để xem chi tiết lỗi
2. Xem lại phần xử lý sự cố bên trên
3. Liên hệ team phát triển kèm log và cấu hình phần cứng

---

**Cập nhật lần cuối:** 2026-04-28  
**Phiên bản:** 3.0  
**Trạng thái:** Đang phát triển
├── tests/
│   ├── Domain.Tests/
│   └── Application.Tests/
├── Rule.md
└── README.md

```

---

## Yêu cầu hệ thống

| Thành phần | Yêu cầu tối thiểu |
|---|---|
| OS | Windows 10 64-bit trở lên |
| .NET | .NET 7.0 hoặc cao hơn |
| RAM | 4 GB |
| USB Camera | Độ phân giải tối thiểu 720p |
| Cổng COM | Tối thiểu 2 cổng (1 cho G6T, 1 cho DUT) |
| Thư viện | OpenCvSharp4 · Microsoft.Extensions.Logging |

---

## Cài đặt & chạy

```bash
# 1. Clone project
git clone https://github.com/your-org/FCT-G6T.git
cd FCT-G6T

# 2. Restore packages
dotnet restore

# 3. Build
dotnet build

# 4. Chạy
dotnet run --project src/Presentation
```

> **Lưu ý:** Cắm USB camera và board G6T trước khi khởi động ứng dụng.

---

## Hướng dẫn sử dụng nhanh (cho công nhân)

```
1. Mở phần mềm FCT-G6T
2. Chọn cổng COM cho G6T  →  cổng COM cho thiết bị
3. Chọn loại thiết bị cần kiểm tra
4. Nhấn [START TEST]
5. Làm theo hướng dẫn hiển thị trên màn hình từng bước
6. Chờ kết quả:  ✅ PASS  hoặc  ❌ FAIL
7. Ghi nhận kết quả, lấy thiết bị tiếp theo
```

---

## Định dạng kịch bản test (`test-cases.json`)

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
      "timeout": 5000
    },
    {
      "order": 2,
      "description": "Đọc tín hiệu phản hồi từ thiết bị",
      "command": "READ_STATUS",
      "expectedResponse": "ALARM_ON",
      "timeout": 3000
    },
    {
      "order": 3,
      "description": "Reset thiết bị về trạng thái bình thường",
      "command": "GPIO_SET 1 LOW",
      "expectedLed": "GREEN",
      "timeout": 3000
    }
  ]
}
```

---

## Luồng xử lý một bài test

```
Operator nhấn [START TEST]
    │
    ▼
TestOrchestrator chạy từng TestStep
    │
    ├─► G6TAdapter.SendCommand()       ── gửi lệnh GPIO qua COM
    │
    ├─► DeviceAdapter.ReadResponse()   ── đọc phản hồi từ DUT
    │
    ├─► LedDetector.Detect(frame)      ── camera detect màu LED
    │
    └─► So sánh Expected vs Actual
            │
            ├── Khớp  →  PASS bước này, sang bước tiếp
            └── Không khớp  →  FAIL, ghi log nguyên nhân
                                │
                                ▼
                        Hiển thị kết quả lên UI
                        Ghi log CSV / SQLite
```

---

## Quy ước kiến trúc & coding convention (theo `Rule.md`)

### Clean Architecture 5 layer

- `Presentation`: WinForms/UserControl, không chứa business logic.
- `Application`: Orchestrator/Service, không import thư viện hardware.
- `Domain`: Model/Interface thuần C#, không phụ thuộc NuGet.
- `Infrastructure`: Cài đặt interface, có thể dùng OpenCvSharp/SQLite.
- `HAL`: Bọc driver/SDK, không có nghiệp vụ.

Luồng đúng: `UI -> Application -> Infrastructure -> HAL`.

### Dependency Injection

- Dùng constructor injection.
- UI không `new` trực tiếp lớp `Infrastructure`/`HAL`.

### Async/Await

- I/O (camera/UART/file) phải async.
- Không dùng `.Result`/`.Wait()`.
- `Infrastructure`/`HAL` dùng `ConfigureAwait(false)`.

### Camera/UI threading

- Update UI phải qua `Invoke`.
- Không dùng `Thread.Sleep` trong loop; dùng `CancellationToken`.
- `Bitmap` cũ phải `Dispose` trước khi gán mới.

### Test case

- Mỗi test case kế thừa `ITestStrategy`.
- Dữ liệu test lấy từ `config/test-cases.json`, không hardcode.

### Lỗi & logging

- Không catch `Exception` chung chung.
- Log rõ `DeviceIndex`/`ComPort`.
- `TestCase` fail phải có `Message` mô tả.

## Đóng góp & phát triển

- Coding convention xem tại [`Rule.md`](./Rule.md)
- Thêm thiết bị mới: tạo file JSON trong `config/` và implement `ITestStrategy`
- Thêm loại phát hiện LED mới: implement `ILedDetector` trong `Infrastructure/Camera/`

---

## Liên hệ

| | |
|---|---|
| Project | FCT-G6T Functional Circuit Test |
| Đối tượng | Công nhân vận hành kiểm tra thiết bị báo cháy |
| Phiên bản | 1.0.0 |

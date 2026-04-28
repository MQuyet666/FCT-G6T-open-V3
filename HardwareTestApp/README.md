# FCT-G6T — Functional Circuit Test (Automated Fire Detection Device Testing)

> Phần mềm kiểm tra chức năng thiết bị báo cháy tự động dành cho công nhân vận hành dây chuyền sản xuất.

**Automated testing application for fire safety detection devices (smoke detectors, heat detectors, alarm bells, buttons) with real-time LED verification via USB camera and hardware-in-the-loop control via UART communication.**

---

## 📋 Mục tiêu / Objectives

FCT-G6T giúp công nhân kiểm tra nhanh các thiết bị phòng cháy chữa cháy trước khi xuất xưởng. Mỗi thiết bị trải qua một bộ bài test tự động, kết quả trả về **PASS / FAIL** rõ ràng mà không yêu cầu công nhân có kiến thức kỹ thuật sâu.

**Purpose:** Enable production line operators to rapidly verify fire detection device functionality with minimal technical knowledge. Automated test suite executes with clear **PASS / FAIL** results for each device type.

---

## ✨ Tính năng chính / Key Features

| Tính năng / Feature | Mô tả / Description |
|---|---|
| **Chọn loại thiết bị / Device Selection** | Đầu báo khói · Đầu báo nhiệt · Chuông đèn · Nút bấm (Smoke Detector, Heat Detector, Alarm Bell, Push Button) |
| **Cấu hình cổng COM / COM Port Setup** | Setup riêng cho G6T board và DUT (Device Under Test) với baud rate tùy chỉnh |
| **Kiểm tra tự động / Automated Testing** | Thực thi từng test step theo kịch bản JSON, hiển thị tiến độ real-time |
| **Phát hiện LED bằng camera / LED Detection via Camera** | OpenCV phân tích khung hình từ USB camera (1280×720 @ 30 FPS) để detect LED (HSV color matching) |
| **Kết quả PASS / FAIL / Results** | Hiển thị trực quan, ghi log chi tiết với timestamp, hex dump UART traffic |
| **Điều khiển GPIO / GPIO Control** | PC ↔ G6T board giao tiếp UART, kiểm soát relay, đọc tín hiệu GPIO |
| **Ghi log tự động / Automatic Logging** | Daily rolling logs (`logs/fct-{yyyy-MM-dd}.log`), retention 30 days |

---

## 🏗️ Kiến trúc hệ thống / System Architecture

### **Sơ đồ Vật Lý / Physical Diagram**

```
┌──────────────────────────────────────────────────────┐
│            PC (FCT-G6T WinForms Application)         │
│                                                      │
│  ┌─────────────────────────────────────────────┐    │
│  │   Presentation Layer (WinForms UI)          │    │
│  │   • Mainform (device selection, buttons)    │    │
│  │   • CameraPreviewControl (live camera)      │    │
│  └──────────────────┬──────────────────────────┘    │
│                     │                                │
│  ┌──────────────────▼──────────────────────────┐    │
│  │   Application Layer                         │    │
│  │   • TestOrchestrator                        │    │
│  │   • SmokeDeviceTestService                  │    │
│  │   • CameraPreviewAppService                 │    │
│  └──────────────────┬──────────────────────────┘    │
│                     │                                │
│  ┌──────────────────▼──────────────────────────┐    │
│  │   Infrastructure Layer                      │    │
│  │   • G6TAdapter (UART control)               │    │
│  │   • DetectorAdapter (UART data readout)     │    │
│  │   • SdkCameraAdapter (DVPCamera SDK)        │    │
│  │   • JsonTestCaseProvider (load config)      │    │
│  │   • FileLogger (rolling logs)               │    │
│  └──────────────────┬──────────────────────────┘    │
│                     │                                │
│  ┌──────────────────▼──────────────────────────┐    │
│  │   HAL Layer (Hardware Abstraction)          │    │
│  │   • SerialPortWrapper (System.IO.Ports)     │    │
│  │   • DVPCamera SDK (native DLL)              │    │
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
    │   Dual UART Communication      │
    │   Frame + CRC validation       │
    │   ACK/Timeout handling         │
    └────────┬──────────────┬────────┘
             │              │
      ┌──────▼──┐      ┌────▼──────┐
      │ G6T     │      │   DUT     │
      │ Board   │      │  Device   │
      │ (GPIO   │      │  (Sensor  │
      │ Control)│      │  Readout) │
      └─────────┘      └───────────┘

   Camera: USB Video Device (1280×720 @ 30 FPS)
   LED Detection: OpenCV + HSV color matching
```

### **Clean Architecture 5 Layers**

```
┌─────────────────────────────────────────────────────┐
│ 5. PRESENTATION                                     │
│    (WinForms UI, user-facing components)            │
├─────────────────────────────────────────────────────┤
│ 4. APPLICATION                                      │
│    (Business logic orchestration, test services)    │
├─────────────────────────────────────────────────────┤
│ 3. DOMAIN                                           │
│    (Pure logic, models, interfaces—no dependencies) │
├─────────────────────────────────────────────────────┤
│ 2. INFRASTRUCTURE                                   │
│    (Adapt domain interfaces to external services)   │
├─────────────────────────────────────────────────────┤
│ 1. HAL (Hardware Abstraction Layer)                │
│    (Thin wrapper over hardware/OS APIs)             │
└─────────────────────────────────────────────────────┘

Key Rules:
✅ UI calls Application (via interfaces)
✅ Application calls Domain & Infrastructure
✅ Infrastructure adapts Domain interfaces
✅ No backwards dependency
⛔ UI cannot directly call Infrastructure/HAL
✅ Constructor injection, no service locator
✅ Async/await throughout (no .Wait()/.Result)
```

---

## 📦 Các thiết bị được hỗ trợ / Supported Devices

| Thiết bị / Device | Mã / Code | Bài test / Test Type |
|---|---|---|
| Đầu báo khói / Smoke Detector | `SMOKE` | Kích thử báo động, kiểm tra LED (RED), đọc tín hiệu phản hồi |
| Đầu báo nhiệt / Heat Detector | `HEAT` | Kích thử báo động, kiểm tra LED (YELLOW), đọc nhiệt độ |
| Chuông đèn / Alarm Bell/Light | `BELL` | Kích chuông (LoRa), detect đèn nhấp nháy, đo thời gian phản hồi |
| Nút bấm / Push Button | `BUTTON` | Nhấn GPIO, kiểm tra tín hiệu trả về, LED xác nhận |

---

## 🗂️ Cấu trúc thư mục / Project Structure

```
HardwareTestApp/
├── Program.cs                      # Entry point, DI container setup
├── README.md                       # This file
├── Rule.md                         # Coding conventions & architecture rules
├── HardwareTestApp.csproj         # Project file (net7.0-windows, WinForms)
│
├── src/                            # Source code (5-layer architecture)
│   ├── Presentation/               # Layer 5: WinForms UI
│   │   ├── Forms/
│   │   │   ├── Mainform.cs        # Main window (device selection, test control)
│   │   │   ├── Mainform.Designer.cs
│   │   │   └── Mainform.resx      # Embedded resources
│   │   └── Controls/
│   │       └── CameraPreviewControl.cs  # Live camera preview user control
│   │
│   ├── Application/                # Layer 4: Business logic services
│   │   ├── Services/
│   │   │   ├── TestOrchestrator.cs      # G6T command dispatcher
│   │   │   ├── SmokeDeviceTestService.cs # Main test execution engine
│   │   │   └── CameraPreviewAppService.cs # UI-safe camera wrapper
│   │   └── Interfaces/
│   │       ├── ITestCaseProvider.cs
│   │       ├── ICameraPreviewAppService.cs
│   │       ├── ISmokeDeviceTestService.cs
│   │       └── IComPortProvider.cs
│   │
│   ├── Domain/                     # Layer 3: Pure domain logic (no dependencies)
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
│   ├── Infrastructure/             # Layer 2: External service implementations
│   │   ├── Serial/
│   │   │   ├── G6TAdapter.cs       # UART communication with G6T board
│   │   │   ├── DetectorAdapter.cs  # UART communication with DUT device
│   │   │   └── ComPortProvider.cs  # List available COM ports
│   │   ├── Camera/
│   │   │   ├── SdkCameraAdapter.cs # DVPCamera SDK integration
│   │   │   └── OpenCvCameraAdapter.cs # Alternative OpenCV implementation
│   │   ├── Configuration/
│   │   │   └── JsonTestCaseProvider.cs # Load test cases from JSON files
│   │   └── Logging/
│   │       ├── FileLogger.cs
│   │       └── FileLoggerProvider.cs  # Custom ILoggerProvider
│   │
│   └── HAL/                        # Layer 1: Hardware Abstraction Layer
│       ├── SerialPortWrapper.cs    # Wrapper over System.IO.Ports.SerialPort
│       ├── ISerialPortWrapper.cs   # Abstract interface
│       └── sdk1/                   # DVPCamera SDK binaries
│           ├── DVPCameraCS64.dll   # Managed C# wrapper
│           ├── DVPCamera64.dll     # Native camera driver
│           └── DVPCameraTL64.cti   # Camera transport layer
│
├── config/                         # Configuration & test definitions
│   ├── appsettings.json           # Runtime settings (timeouts, baud rates, etc.)
│   ├── camera.json                # Camera device index & resolution
│   ├── smoke-test-cases.json      # Test scenarios for smoke detectors
│   ├── heat-test-cases.json       # Test scenarios for heat detectors
│   ├── bell-test-cases.json       # Test scenarios for alarm bells
│   └── button-test-cases.json     # Test scenarios for buttons
│
├── logs/                           # Auto-generated daily log files
│   ├── fct-2026-04-28.log
│   └── fct-2026-04-27.log
│
├── bin/Debug & bin/Release/       # Compiled output
└── obj/                            # Build artifacts
```

---

## 🛠️ Technology Stack & Dependencies

### **.NET & Core Frameworks**

| Package | Version | Purpose |
|---------|---------|---------|
| **.NET** | 7.0-windows | Desktop application runtime |
| **WinForms** | (implicit in 7.0-windows) | UI framework |
| **System.IO.Ports** | 8.0.0 | Serial port communication (UART) |

### **Dependency Injection & Configuration**

| Package | Version | Purpose |
|---------|---------|---------|
| **Microsoft.Extensions.DependencyInjection** | 8.0.0 | DI container (service registration) |
| **Microsoft.Extensions.Configuration** | 8.0.0 | Settings management |
| **Microsoft.Extensions.Configuration.Json** | 8.0.0 | JSON config file binding |
| **Microsoft.Extensions.Configuration.Binder** | 8.0.0 | Type-safe config deserialization |
| **Microsoft.Extensions.Logging** | 8.0.0 | Logging abstraction |
| **Microsoft.Extensions.Logging.Abstractions** | 8.0.0 | Logger interfaces |

### **Image Processing & Computer Vision**

| Package | Version | Purpose |
|---------|---------|---------|
| **OpenCvSharp4** | 4.13.0.20260302 | OpenCV bindings (LED detection via HSV color matching) |
| **OpenCvSharp4.runtime.win** | 4.13.0.20260302 | OpenCV native libraries (Windows x64) |

### **Hardware SDKs**

| Package | Type | Purpose |
|---------|------|---------|
| **DVPCameraCS64** | Local DLL in `src/HAL/sdk1/` | Hikvision DVP Camera SDK (proprietary) |
| **System.IO.Ports** | Built-in .NET | Serial port communication for UART |

---

## ⚙️ Configuration Files / Tệp cấu hình

### **1. `appsettings.json`** — Runtime Settings

```json
{
  "Serial": {
    "G6tBaudRate": 9600,              // G6T board baud rate
    "DetectorBaudRate": 9600,         // DUT device baud rate
    "ReadTimeoutMs": 100,             // Serial read timeout (ms)
    "WriteTimeoutMs": 1000,           // Serial write timeout (ms)
    "FrameRetryCount": 1,             // G6T frame retries
    "DetectorRetryCount": 1,          // DUT frame retries
    "FrameAckTimeoutSeconds": 3,      // G6T ACK timeout (seconds)
    "DetectorAckTimeoutSeconds": 3    // DUT ACK timeout (seconds)
  },
  "TestTimeouts": {
    "CommandAckTimeoutSeconds": 3,    // G6T command ACK wait time
    "LedDetectTimeoutSeconds": 5,     // LED detection wait time (from camera)
    "ButtonTestTimeoutSeconds": 15,   // Button test response timeout
    "LedDetectPollDelayMs": 100       // Poll interval for LED detection
  },
  "CameraRuntime": {
    "FrameBufferSize": 3,             // Max queued frames in buffer
    "CameraRetryIntervalSeconds": 2   // Retry interval if camera unavailable
  },
  "Logging": {
    "Directory": "logs",              // Log output directory
    "FilePrefix": "fct-",             // Log filename prefix
    "RetentionDays": 30               // Auto-delete logs older than 30 days
  }
}
```

### **2. `camera.json`** — Camera Configuration

```json
{
  "DeviceIndex": 0,      // USB camera device index (0 = first camera)
  "Width": 1280,         // Frame width (pixels)
  "Height": 720,         // Frame height (pixels)
  "TargetFps": 30        // Target frames per second
}
```

### **3. Test Case Files** — Device-Specific Workflows

Example: `smoke-test-cases.json`

```json
[
  {
    "id": "TC_SMOKE_01",
    "name": "Power On LED Test",
    "deviceType": "SMOKE",
    "steps": [
      {
        "order": 1,
        "description": "Enable power and wait for red LED",
        "command": "POWER_ON",
        "expectedLed": "RED",
        "timeout": 5000,
        "maxRetry": 0
      },
      {
        "order": 2,
        "description": "Test button buzzer (DUT smoke sensor)",
        "command": "TEST_BUTTON_BUZZER",
        "expectedLed": "",
        "timeout": 15000,
        "maxRetry": 1
      }
    ],
    "teardown": [
      {
        "order": 1,
        "description": "Power off device",
        "command": "POWER_OFF",
        "expectedLed": "",
        "timeout": 3000,
        "maxRetry": 0
      }
    ]
  }
]
```

**Structure:**

- **`steps`** — Main test sequence (executed in order)
- **`teardown`** — Cleanup operations (always executed, even on failure)
- **`expectedLed`** — Expected LED color (RED, YELLOW, GREEN, or empty)
- **`timeout`** — Milliseconds to wait for step completion
- **`maxRetry`** — Number of retries if step fails

---

## 🚀 Build & Run / Xây dựng & chạy

### **Prerequisites / Yêu cầu**

- **Windows 10/11** (x64)
- **.NET SDK 7.0** or later
- **Visual Studio 2022** (or VS Code + C# Dev Kit)
- **USB Camera** (DVPCamera SDK compatible, typically HikVision cameras)
- **Hardware:** G6T board, DUT device, 2× USB-to-UART adapters (for COM ports)

### **Build**

```powershell
# Restore NuGet packages
dotnet restore

# Build in Debug mode
dotnet build --configuration Debug

# Build in Release mode (optimized)
dotnet build --configuration Release
```

### **Run**

```powershell
# Run directly
dotnet run --project HardwareTestApp.csproj

# Or run compiled executable
./bin/Debug/net7.0-windows/HardwareTestApp.exe
```

### **Build & Run from VS Code**

Press `Ctrl+Shift+B` to see available build tasks, or `F5` to debug (requires launch configuration).

---

## 🧪 How to Use / Cách sử dụng

### **Step-by-Step Test Procedure**

1. **Prepare Hardware / Chuẩn bị phần cứng**
   - Connect G6T board to PC via USB-UART adapter (note the COM port, e.g., COM3)
   - Connect DUT device to PC via separate USB-UART adapter (note the COM port, e.g., COM4)
   - Mount USB camera pointing at device under test
   - Position camera so LED is in Region of Interest (ROI)
   - Power up hardware (G6T board and DUT should be powered)

2. **Launch Application / Khởi động ứng dụng**

   ```powershell
   dotnet run --project HardwareTestApp.csproj
   ```

3. **Configure COM Ports / Cấu hình cổng COM**
   - **G6T COM Port:** Select from dropdown (e.g., COM3)
   - **DUT COM Port:** Select from dropdown (e.g., COM4)

4. **Select Device Type / Chọn loại thiết bị**
   - Radio buttons: Smoke Detector, Heat Detector, Alarm Bell, Button
   - Example: Select "Smoke Detector" for smoke sensor test

5. **Adjust Camera ROI (optional) / Điều chỉnh vùng quan sát camera**
   - Live camera preview shows in control
   - Click + drag on preview to reposition ROI rectangle
   - ROI border turns **GREEN** when LED detected, **RED** if no LED

6. **Click START / Nhấn nút START**
   - Test runs automatically: power on → capture LED → test button → power off
   - Progress log updates in real-time (RTB textbox)
   - LED status indicator: Red (OFF), Green (ON)

7. **View Results / Xem kết quả**
   - **PASS** → All test steps passed, device ships
   - **FAIL** → One or more steps failed, device rejected with reason logged
   - Check `logs/fct-{yyyy-MM-dd}.log` for detailed troubleshooting

8. **Repeat or Stop / Lặp lại hoặc dừng**
   - Click START again to run next device test
   - Close application to stop (logs auto-saved)

---

## 🔧 Development Guide / Hướng dẫn phát triển

### **Code Conventions (from `Rule.md`)**

✅ **Clean Architecture 5 Layers**

- Strict separation: UI → Application → Domain ← Infrastructure ← HAL
- No backward dependencies

✅ **Dependency Injection**

- All services registered in `Program.cs` DI container
- No `new` keyword in production code (factories only)
- Constructor injection enforced

✅ **Async/Await**

- All I/O operations async: `Task`, `Task<T>`
- **Forbidden:** `.Wait()`, `.Result`, `.GetAwaiter().GetResult()`
- Use `await` at every level

✅ **Camera UI Updates**

- All frame updates use `Control.InvokeRequired` check
- Never update UI from background thread directly
- Use `this.Invoke(delegate { /* UI update */ })` pattern

✅ **Test Concurrency**

- Only **1 test runs at a time**
- START button disabled while test running
- Prevents race conditions in UART communication

✅ **Hardware Configuration**

- **No hardcoded COM ports** — Always read from config/dropdown
- **No hardcoded camera index** — Read from `camera.json`
- **No hardcoded timeouts** — Always from `appsettings.json`

### **Adding a New Device Type**

1. **Create test case JSON** (e.g., `config/my-device-test-cases.json`)

   ```json
   [
     {
       "id": "TC_MYDEV_01",
       "name": "My Device Test",
       "deviceType": "MYDEVICE",
       "steps": [ ... ],
       "teardown": [ ... ]
     }
   ]
   ```

2. **Add to Application Service** (e.g., `SmokeDeviceTestService.cs`)
   - Add case in switch statement for new `deviceType`
   - Reuse `TestOrchestrator` for G6T commands
   - Reuse `DetectorAdapter` for DUT reading

3. **Update UI** (`Mainform.cs`)
   - Add radio button or dropdown option for new device type
   - Bind selection to `deviceType` variable

4. **Test Thoroughly**
   - Verify UART communication logs (check `logs/fct-*.log`)
   - Verify LED detection with camera preview
   - Test timeout & retry scenarios

### **How to Add a New G6T Command**

1. **Add to enum** (`Domain/Models/G6TCommandId.cs`)

   ```csharp
   public enum G6TCommandId
   {
       PowerControl = 0x01,
       TestButton = 0x02,
       MyNewCommand = 0x0A  // ← Add here
   }
   ```

2. **Add to `TestOrchestrator`** (`Application/Services/TestOrchestrator.cs`)

   ```csharp
   public async Task<G6TResponse> MyNewCommandAsync()
   {
       var command = new G6TCommand 
       { 
           CommandId = G6TCommandId.MyNewCommand,
           Data = new byte[] { /* payload */ }
       };
       return await _g6tAdapter.SendCommandAsync(command, cancellationToken);
   }
   ```

3. **Call from Service**

   ```csharp
   var response = await _testOrchestrator.MyNewCommandAsync();
   ```

### **Logging Best Practices**

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
        _logger.LogInformation("Starting operation");
        try
        {
            // ... do work
            _logger.LogInformation("Operation completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed");
            throw;
        }
    }
}
```

Logs go to `logs/fct-{yyyy-MM-dd}.log` (daily rolling).

---

## 🔌 Hardware Setup / Cấu hình phần cứng

### **UART Communication Protocol**

**G6T Board & DUT Device** both use custom framed UART protocol:

```
Frame Format:
┌─────────┬─────────┬────────┬──────────┬─────┐
│ 0x1F    │ 0x2F    │ 0x3F   │ 0xFF     │ Cmd │
│ (preamble, 4 bytes)                   │ ID  │
└─────────┴─────────┴────────┴──────────┴─────┤
  ↓
┌────────┬──────────┬─────────┬────────┐
│ Length │ Data...  │ CRC16   │ Ack    │
│ (1B)   │ (NB)     │ (2B)    │ (Opt)  │
└────────┴──────────┴─────────┴────────┘

Baud Rate: 9600 (default, configurable)
Parity: None
Stop Bits: 1
Data Bits: 8
Timeout: 3 seconds (configurable)
```

### **G6T Board Commands**

| Command | Code | Data | Purpose |
|---------|------|------|---------|
| **PowerControl** | 0x01 | `{On/Off}` | Enable/disable power to DUT |
| **TestButton** | 0x02 | `{}` | Test buzzer/relay |
| **SetCalibPin** | 0x03 | `{Set/Unset}` | Calibration pin control |
| **TestButton2** | 0x04 | `{}` | GPIO pin 2 test |
| **TestButton3** | 0x05 | `{}` | GPIO pin 3 test |

### **DUT Sensor Readings**

| Reading | Timeout | Response Type |
|---------|---------|---|
| **Temperature** | 3 seconds | `DetectorResponse { Temperature: float }` |
| **Smoke Level** | 3 seconds | `DetectorResponse { SmokeLevel: int }` |
| **LoRa Value** | 3 seconds | `DetectorResponse { LoraValue: int }` |

---

## 🎥 Camera & LED Detection / Phát hiện LED bằng camera

### **Hardware**

- **Camera:** USB video device (HikVision DVPCamera SDK compatible)
- **Resolution:** 1280×720 (configured in `camera.json`)
- **Frame Rate:** 30 FPS (configurable)
- **Mounting:** Fixed position, LED in frame center

### **LED Detection Algorithm**

**Color-based detection via HSV:**

1. **Capture frame** from camera (BGR24 format)
2. **Convert to HSV** color space
3. **Define color range** for expected LED color (e.g., RED: H 0-10 or 170-180, S 100-255, V 100-255)
4. **Mask frame** with color range → binary image
5. **Count white pixels** in ROI
6. **Result:** If pixel count > threshold → LED ON (PASS), else LED OFF (FAIL)

**Expected LED Colors:**

- **SMOKE detector:** RED LED (H: 0-10°, S: 100-255, V: 100-255)
- **HEAT detector:** YELLOW LED (H: 20-30°, S: 100-255, V: 100-255)
- **ALARM bell:** GREEN/YELLOW flashing (motion-based detection)
- **BUTTON:** GREEN confirmation LED

### **Region of Interest (ROI) Adjustment**

Users can click + drag on camera preview to reposition ROI rectangle:

- **Default:** Center of frame, 100×100 pixels
- **User adjusts:** Click + drag to move rectangle
- **Visual feedback:**
  - **GREEN border** when LED detected in ROI
  - **RED border** when no LED in ROI

---

## 📊 Logging & Diagnostics / Ghi log & chẩn đoán

### **Log File Location**

```
logs/
├── fct-2026-04-28.log  (today)
├── fct-2026-04-27.log  (yesterday)
└── ...
```

Daily rolling logs, auto-delete after 30 days (configurable in `appsettings.json`).

### **Log Levels & Examples**

| Level | Example | When |
|-------|---------|------|
| **Information** | `[2026-04-28 10:45:23.123] INFO: Starting test sequence for SMOKE device` | Normal flow |
| **Warning** | `[2026-04-28 10:46:15.456] WARN: LED detection timeout (attempt 2/3), retrying...` | Retries, timeouts |
| **Error** | `[2026-04-28 10:47:02.789] ERROR: Camera not found (DeviceIndex=0), aborting test` | Failures, exceptions |

### **Hex Dump Example**

```
[2026-04-28 10:45:30.123] INFO: G6T TX: 1F 2F 3F FF 01 01 01 AA BB
[2026-04-28 10:45:30.456] INFO: G6T RX: 1F 2F 3F FF 01 01 01 00 CC DD
```

(TX = transmitted, RX = received)

---

## 🐛 Troubleshooting / Giải quyết vấn đề

| Issue | Cause | Solution |
|-------|-------|----------|
| **"COM port not found"** | USB adapter disconnected or wrong port selected | Reconnect adapter, rescan COM ports, select correct port |
| **"LED detection timeout"** | Camera not capturing frames or LED not visible | Check camera mounted correctly, verify lighting, adjust ROI |
| **"G6T ACK timeout"** | G6T board not responding, baud rate mismatch | Power cycle G6T, check `appsettings.json` baud rate matches board |
| **"Camera SDK initialization failed"** | DVPCamera DLL not found in `src/HAL/sdk1/` | Verify DLL files present, reinstall camera driver |
| **"Test hangs"** | START button still disabled, previous test not completed | Wait for test to finish, check logs for stuck step |
| **Logs not generated** | Log directory missing or permission issue | Check `logs/` folder exists, verify write permissions |

**Debug Tips:**

1. Open `logs/fct-*.log` during/after test to see detailed hex dumps
2. Enable verbose logging: Check `appsettings.json` log level (if available)
3. Verify hardware: Test UART with external serial monitor tool
4. Check camera separately: Use camera manufacturer's software to verify it works

---

## 📝 Project Rules & Conventions / Quy tắc & hội ước

See [Rule.md](Rule.md) for detailed coding standards:

- Clean Architecture 5 layers
- Constructor injection required
- No hardcoded values (camera index, timeout, COM port)
- Async/await throughout
- UI updates via Invoke()
- Comprehensive logging

---

## 📄 License & Contributing / Giấy phép & Đóng góp

**License:** [Specify your license, e.g., MIT, Proprietary, etc.]

**Contributing:** Please follow coding conventions in `Rule.md` before submitting PRs.

---

## 📧 Support / Hỗ trợ

For issues, questions, or feature requests:

1. Check logs in `logs/fct-*.log` for error details
2. Review troubleshooting section above
3. Contact the development team with log files and hardware configuration

---

**Last Updated:** 2026-04-28  
**Version:** 3.0  
**Status:** Active Development
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

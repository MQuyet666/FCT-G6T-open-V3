# FCT-G6T — Functional Circuit Test

> Phần mềm kiểm tra chức năng thiết bị báo cháy tự động dành cho công nhân vận hành dây chuyền sản xuất.

---

## Mục tiêu

FCT-G6T giúp công nhân kiểm tra nhanh các thiết bị phòng cháy chữa cháy (đầu báo khói, đầu báo nhiệt, chuông đèn, nút bấm) trước khi xuất xưởng. Mỗi thiết bị trải qua một bộ bài test tự động, kết quả trả về **PASS / FAIL** rõ ràng mà không yêu cầu công nhân có kiến thức kỹ thuật sâu.

---

## Tính năng chính

| Tính năng | Mô tả |
|---|---|
| Chọn loại thiết bị | Đầu báo khói · Đầu báo nhiệt · Chuông đèn · Nút bấm |
| Cấu hình cổng COM | Setup cổng giao tiếp G6T và thiết bị DUT trước khi test |
| Kiểm tra tự động | Chạy từng bài test theo kịch bản định sẵn, hiển thị từng bước |
| Phát hiện LED bằng camera | OpenCV detect trạng thái LED (màu sắc, ON/OFF) qua USB camera |
| Kết quả PASS / FAIL | Hiển thị trực quan, ghi log chi tiết kèm timestamp |
| Điều khiển GPIO | PC giao tiếp với board G6T để kích hoạt / đọc trạng thái các chân GPIO |

---

## Kiến trúc hệ thống

```
┌─────────────────────────────────────────────┐
│                PC (FCT-G6T App)             │
│                                             │
│  ┌──────────┐   UART/COM   ┌─────────────┐  │
│  │  G6T     │◄────────────►│  WinForms   │  │
│  │  Board   │  GPIO control│  UI         │  │
│  └──────────┘              └──────┬──────┘  │
│       │ GPIO                      │         │
│       ▼                    ┌──────▼──────┐  │
│  ┌──────────┐   UART/COM   │  OpenCV     │  │
│  │  DUT     │◄────────────►│  Camera     │  │
│  │  Device  │  data readout│  LED detect │  │
│  └──────────┘              └─────────────┘  │
└─────────────────────────────────────────────┘
```

**PC ↔ G6T Board** — giao tiếp UART, PC gửi lệnh điều khiển GPIO (kích relay, đọc tín hiệu đầu vào).

**PC ↔ DUT** — giao tiếp UART riêng, lấy dữ liệu phản hồi từ thiết bị đang kiểm tra.

**Camera** — USB camera gắn cố định, OpenCvSharp phát hiện trạng thái LED theo màu HSV.

---

## Các thiết bị được hỗ trợ

| Thiết bị | Mã | Bài test |
|---|---|---|
| Đầu báo khói | SMOKE | Kích thử báo động, kiểm tra LED, đọc tín hiệu phản hồi |
| Đầu báo nhiệt | HEAT | Kích thử báo động, kiểm tra LED, đọc tín hiệu phản hồi |
| Chuông đèn | BELL | Kích chuông, detect đèn nhấp nháy, đo thời gian phản hồi |
| Nút bấm | BUTTON | Nhấn GPIO, kiểm tra tín hiệu trả về, LED xác nhận |

---

## Cấu trúc thư mục

```
FCT-G6T/
├── src/
│   ├── Presentation/
│   │   ├── Forms/
│   │   │   └── Mainform.cs           # Form chính — operator sử dụng
│   │   └── Controls/
│   │       └── CameraPreviewControl.cs
│   ├── Application/
│   │   ├── CameraService.cs
│   │   ├── FrameProcessingService.cs
│   │   └── TestOrchestrator.cs
│   ├── Domain/
│   │   ├── Interfaces/
│   │   │   ├── ICameraService.cs
│   │   │   ├── IDeviceAdapter.cs
│   │   │   └── ILedDetector.cs
│   │   └── Models/
│   │       ├── TestCase.cs
│   │       ├── TestResult.cs
│   │       ├── LedResult.cs
│   │       └── CameraConfig.cs
│   ├── Infrastructure/
│   │   ├── Camera/
│   │   │   ├── OpenCvCameraAdapter.cs
│   │   │   ├── LedColorDetector.cs
│   │   │   └── BitmapConverter.cs
│   │   └── Serial/
│   │       ├── G6TAdapter.cs
│   │       └── DeviceAdapter.cs
│   └── HAL/
│       ├── CaptureLoop.cs
│       └── SerialPortWrapper.cs
├── config/
│   └── test-cases.json               # Kịch bản test cho từng loại thiết bị
├── tests/
│   ├── Domain.Tests/
│   └── Application.Tests/
├── RULE.md
└── README.md
```

---

## Yêu cầu hệ thống

| Thành phần | Yêu cầu tối thiểu |
|---|---|
| OS | Windows 10 64-bit trở lên |
| .NET | .NET 6.0 hoặc cao hơn |
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

## Đóng góp & phát triển

- Coding convention xem tại [`RULE.md`](./RULE.md)
- Thêm thiết bị mới: tạo file JSON trong `config/` và implement `ITestStrategy`
- Thêm loại phát hiện LED mới: implement `ILedDetector` trong `Infrastructure/Camera/`

---

## Liên hệ

| | |
|---|---|
| Project | FCT-G6T Functional Circuit Test |
| Đối tượng | Công nhân vận hành kiểm tra thiết bị báo cháy |
| Phiên bản | 1.0.0 |

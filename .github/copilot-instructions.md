# Copilot Instructions - FCT-G6T

## Project Guidelines
- Quy định: mỗi lớp có interface `I*.cs` để các lớp gọi qua interface, ví dụ `ISerialPortWrapper`.
- Ưu tiên dùng interface/contract hiện có trước khi tạo class hoặc hàm mới.
- Dependency injection phải được đăng ký tập trung tại `Program.cs` hoặc `CompositionRoot` nếu project đã tách riêng.

## Bắt buộc trước khi code
- Trước khi viết mới, sửa, refactor hoặc review code, phải đọc `Rule.md` ở thư mục gốc project.
- Mọi quyết định về kiến trúc, naming, dependency, async/await, logging, test case và DI phải tuân theo `Rule.md`.
- Nếu yêu cầu của user xung đột với `Rule.md`, phải báo rõ xung đột trước khi sửa code.

## Bắt buộc khi làm việc với layer
- Khi tạo, sửa, review hoặc gọi đến một layer, nếu layer đó có file `skills.md` hoặc thư mục `skills/`, phải đọc skill trước để biết ranh giới layer, API/hàm sẵn có, pattern chuẩn và file tham chiếu cần dùng.
- Không tự đoán hàm, class hoặc adapter trong layer khi chưa kiểm tra skill và code hiện có.
- Chỉ gọi layer thông qua contract/interface đúng kiến trúc. Không gọi trực tiếp xuống layer thấp hơn nếu `Rule.md` hoặc skill của layer cấm.

### Skill hiện có trong project
- HAL: đọc `src/HAL/skills.md` trước khi làm việc với `FCT.G6T.HAL.*`, camera loop, serial wrapper, GPIO driver wrapper hoặc SDK/driver phần cứng.
- Infrastructure: đọc `src/Infrastructure/skills.md`, sau đó đọc skill chính `src/Infrastructure/skills/infrastructure-layer/SKILL.md` trước khi làm việc với adapter camera, serial adapter, configuration, logging, DI wiring hoặc bất kỳ implementation nào thuộc `FCT.G6T.Infrastructure.*`.

## Quy trình tối thiểu trước khi sửa code
1. Đọc `Rule.md`.
2. Xác định layer/file bị ảnh hưởng.
3. Nếu layer có skill, đọc skill tương ứng trước khi code.
4. Kiểm tra interface/contract hiện có trong layer thay vì tạo class hoặc hàm mới ngay.
5. Sửa code theo đúng dependency flow và đăng ký DI tại `Program.cs` nếu có dependency mới.

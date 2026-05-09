# SLMS Automation Implementation Plan

## 1. Overview
Dự án cần tích hợp thêm một luồng tự động hoá (Automation) cho ứng dụng desktop **SmartLMS** (SLMS). Mục đích là để điều khiển mở app, đăng nhập tự động, xuất file dữ liệu Stencil ra định dạng Excel, và sau đó đọc file Excel này để cập nhật (hoặc thêm mới) vào bảng `mex_mes.tb_spec_stencil`. 
Vì ứng dụng GMES hiện tại cũng đang chạy một luồng tự động, cả hai luồng sẽ chạy độc lập theo 2 timer khác nhau nhưng phải **tuần tự** (không chạy song song) để tránh xung đột hệ thống và giả lập phím/chuột.

## 2. Project Type
**BACKEND** (C# Console Application with Desktop Automation)

## 3. Success Criteria
- [x] Mở ứng dụng `SmartLMS.exe` thành công.
- [x] Giả lập bàn phím và chuột để thực hiện đăng nhập và xuất file Excel dựa trên toạ độ một cách chính xác.
- [x] Tránh lưu trùng file bằng cách đặt tên file theo giờ hệ thống.
- [x] Parsing thành công file Excel lấy ra `STENCIL_ID`, `TOTAL_COUNT`, và `DATE_RECEIVE` (từ cột Last update).
- [x] Import dữ liệu vào Database: Replace `TOTAL_COUNT` và `DATE_RECEIVE` nếu `STENCIL_ID` tồn tại, ngược lại Insert mới.
- [x] Tính năng chạy song song nhưng có cơ chế khoá (Mutex/SemaphoreSlim) để đảm bảo 2 app không thao tác chồng chéo.

## 4. Tech Stack
- **C# 11/.NET 7+**: Framework hiện tại của project.
- **P/Invoke (user32.dll)** / `WindowsInput` / `SendKeys`: Sử dụng các hàm `SetCursorPos`, `mouse_event`, `keybd_event` để gửi thao tác click và phím cơ bản.
- **System.Threading.SemaphoreSlim**: Dùng làm khoá toàn cục (lock) quản lý đồng bộ giữa Task chạy GMES và Task chạy SLMS.
- **EPPlus / ExcelDataReader**: Dùng thư viện đọc Excel hiện tại của hệ thống để phân tích file xuất ra.
- **MySqlConnector**: Cập nhật Database.

## 5. File Structure
Thêm và cập nhật các file sau:
- `src/GmesImporter.App/appsettings.json`: Thêm block `Slms` chứa đường dẫn EXE, toạ độ, credentials và thời gian chạy.
- `src/GmesImporter.App/Configuration/AppSettings.cs`: Cập nhật thêm class `SlmsSettings`.
- `src/GmesImporter.Core/Native/Win32Input.cs` (Hoặc thêm class helper): Class wrapper chứa các hàm gọi P/Invoke để thao tác chuột và phím.
- `src/GmesImporter.App/Workflow/SlmsDesktopAutomation.cs`: Workflow mở ứng dụng và thao tác tuần tự từng bước (1->8).
- `src/GmesImporter.App/Database/MySqlStencilRepository.cs`: Logic tương tác Database cập nhật bảng `tb_spec_stencil`.
- `src/GmesImporter.App/Workflow/SlmsImportRunner.cs`: Logic đọc file Excel, gọi DbRepository và quản lý Retry/Exception (tương tự `ImportRunner`).
- `src/GmesImporter.App/Workflow/ApplicationLoopCoordinator.cs`: Quản lý SemaphoreSlim để hai tiến trình GMES và SLMS luân phiên chờ và chạy.
- `src/GmesImporter.App/Program.cs`: Thay đổi hàm `Main` từ việc gọi 1 vòng lặp sang gọi 2 vòng lặp qua `ApplicationLoopCoordinator`.

## 6. Task Breakdown

### Task 1: Cấu hình và AppSettings [Agent: backend-specialist]
- **INPUT**: `appsettings.json` hiện tại.
- **OUTPUT**: Bổ sung object `Slms` chứa thông tin như `RunIntervalSeconds`, `ExecutablePath`, `LoginInfo` và `Coordinates`.
- **VERIFY**: `ConfigurationValidator` đọc được file JSON không gặp lỗi, validate đúng định dạng.

### Task 2: P/Invoke Win32 Input Layer [Agent: backend-specialist]
- **INPUT**: Yêu cầu thao tác click tại toạ độ X, Y và thao tác bàn phím (Tab, Enter, text).
- **OUTPUT**: File `Win32Input.cs` chứa các API `SetCursorPos`, `mouse_event` và hàm gõ chuỗi (`SendKeys.SendWait` hoặc P/Invoke).
- **VERIFY**: Có thể dùng để dịch chuyển chuột đến toạ độ màn hình và click thành công.

### Task 3: SLMS Desktop Automation Flow [Agent: backend-specialist]
- **INPUT**: Toạ độ chi tiết từ cấu hình và các bước do User yêu cầu.
- **OUTPUT**: Class `SlmsDesktopAutomation.cs` chứa hàm khởi động Process và chạy chuỗi lệnh tuần tự với Delay phù hợp (tránh quá nhanh). Chức năng nhập tên lưu file theo cú pháp `StencilExport_yyyyMMdd_HHmmss.xlsx`.
- **VERIFY**: File Excel được lưu đúng thư mục quy định.

### Task 4: Parsing Excel & DB Repository [Agent: backend-specialist]
- **INPUT**: File Excel xuất ra từ SLMS.
- **OUTPUT**: Code đọc Excel lấy 3 cột cần thiết (ID, Count, Last Update). Repository thực hiện `INSERT INTO ... ON DUPLICATE KEY UPDATE` hoặc logic `IF EXISTS`.
- **VERIFY**: Chạy Unit test / Dry-run thấy Query update đúng số lượng bản ghi, không lỗi khoá ngoại/type.

### Task 5: SLMS Runner & Sync Coordinator [Agent: backend-specialist]
- **INPUT**: Code GMES Runner cũ và SLMS Runner mới.
- **OUTPUT**: `SlmsImportRunner` và `ApplicationLoopCoordinator` (hoặc update Program.cs). Sử dụng `SemaphoreSlim(1, 1)` để bọc quanh vòng lặp.
- **VERIFY**: Nhìn thấy trong log hai task bắt đầu đếm thời gian riêng, nhưng khi Task GMES đang lấy dữ liệu thì Task SLMS (nếu đến giờ) sẽ nằm chờ cho đến khi GMES xong.

## ✅ PHASE X COMPLETE
- Lint: ✅ Pass
- Security: ✅ No critical issues
- Build: ✅ Success
- Date: 2026-05-08

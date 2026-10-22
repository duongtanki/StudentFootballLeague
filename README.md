# Student Football League Management System (Hệ Thống Quản Lý Giải Bóng Đá Sinh Viên)

Đề tài số 05 — Môn học: **PRN232 - Phát Triển Ứng Dụng Web API & Web Client** (.NET 8.0)

## 📌 Giới Thiệu Đề Tài
Hệ thống Quản lý Giải bóng đá Sinh viên được thiết kế theo kiến trúc tách biệt hoàn toàn giữa Tầng dịch vụ Back-end (**ASP.NET Core Web API**) và Tầng ứng dụng Front-end (**ASP.NET Core MVC**), áp dụng đầy đủ các công nghệ:
- **Back-end:** ASP.NET Core Web API (.NET 8.0), Entity Framework Core (Code-First), Microsoft SQL Server 2022, OData v8, JWT Bearer Token, Swagger/OpenAPI.
- **Front-end:** ASP.NET Core MVC (.NET 8.0), HTML5, CSS3, Bootstrap 5 (Responsive), TagHelper, PartialView, Cookie Authentication / Identity.
- **Tương tác API:** Typed `HttpClient` (`IHttpClientFactory`) kết hợp `DelegatingHandler` tự động đính kèm Token xác thực.

---

## 🏗️ Kiến Trúc Hệ Thống (4 Tầng)
- **`StudentFootballLeague.DataAccess` (DAL):** Quản lý thực thể (`User`, `Tournament`, `Team`, `Player`, `TeamRegistration`, `Match`, `MatchEvent`, `PlayerStat`) và cấu hình `AppDbContext` (Fluent API, Filtered Index tránh lỗi NULL StudentCode, Restrict Cascade Paths).
- **`StudentFootballLeague.BusinessLogic` (BLL):** Chứa các DTOs, Validation logic, `TokenService` (sinh JWT Token), `MatchService` (kiểm tra xung đột sân và lịch thi đấu $\pm 90$ phút), `StandingsService` (tự động tính điểm BXH và điểm kỷ luật Fair-play).
- **`StudentFootballLeague.API`:** Cung cấp các RESTful Endpoints và OData Controllers (`/odata/Teams`, `/odata/Players`, `/odata/Matches`, `/odata/PlayerStats`).
- **`StudentFootballLeague.WebClient`:** WebApp MVC cho người dùng, sử dụng Cookie Identity phân quyền RBAC và gọi API độc lập.

---

## 🚀 Hướng Dẫn Cài Đặt & Khởi Chạy

### 1. Khởi tạo Cơ sở dữ liệu SQL Server
- Mở SQL Server Management Studio (SSMS) hoặc Visual Studio SQL Server Object Explorer.
- Chạy script SQL nằm tại đường dẫn: `Database/StudentFootballLeagueDB.sql`
- Cơ sở dữ liệu `StudentFootballLeagueDB` sẽ được tạo và nạp sẵn dữ liệu mẫu.

### 2. Khởi chạy Web API (Port 7108 / 5207)
```powershell
cd StudentFootballLeague.API
dotnet run
```
Truy cập Swagger UI: [https://localhost:7108/swagger](https://localhost:7108/swagger) hoặc [http://localhost:5207/swagger](http://localhost:5207/swagger)

### 3. Khởi chạy WebClient MVC (Port 7005 / 5237)
Mở một cửa sổ dòng lệnh (terminal) mới:
```powershell
cd StudentFootballLeague.WebClient
dotnet run
```
Truy cập Website: [https://localhost:7005](https://localhost:7005) hoặc [http://localhost:5237](http://localhost:5237)

---

## 🔑 Tài Khoản Dùng Thử Nghiệm (Mật khẩu mặc định: `123456`)
| Vai trò | Email đăng nhập | Mật khẩu | Chức năng chính |
| :--- | :--- | :--- | :--- |
| **Ban tổ chức (Admin)** | `admin@fpt.edu.vn` | `123456` | Quản lý giải đấu, phê duyệt đội bóng, phân chia bảng đấu, lập lịch thi đấu |
| **Trọng tài (Referee)** | `referee1@fpt.edu.vn` | `123456` | Cập nhật trạng thái trận, nhập tỷ số và diễn biến sự kiện bàn thắng/thẻ phạt |
| **Đội trưởng (TeamManager)** | `captain.se@fpt.edu.vn` | `123456` | Quản lý thông tin đội bóng, danh sách cầu thủ (số áo, vị trí), đăng ký giải |
| **Khách (Guest / Sinh viên)** | *(Không cần đăng nhập)* | - | Xem lịch thi đấu, kết quả trực tuyến, BXH các bảng A/B, danh sách Vua phá lưới |

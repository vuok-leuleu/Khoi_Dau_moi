---
trigger: always_on
---

---
description: Quy chuẩn kiến trúc, lập trình C# Unity và quy trình làm việc cho Agent
trigger: always_on
---

# Unity & C# Development Workspace Rules

## 1. Môi trường & Công nghệ (Environment & Tech Stack)
- **Engine:** Unity (Sử dụng C# .NET / Mono / IL2CPP).
- **Kiến trúc Game:** Ưu tiên Component-Based Architecture, ScriptableObjects cho Data-driven design, và Event-driven / Observer pattern cho decoupled UI/Logic.
- **Async & Threading:** Ưu tiên dùng `UniTask` hoặc `async/await` chuẩn của C# cho các tác vụ bất đồng bộ thay vì `Coroutine` truyền thống khi xử lý dữ liệu/logic.

## 2. Quy chuẩn Viết Code C# (C# Coding Standards)
- **Naming Conventions:**
  - `PascalCase`: Tên class, struct, enum, public method, public field, property.
  - `camelCase`: Private/protected fields, tham số hàm (parameters), biến cục bộ (local variables).
  - Phụ tố/Tiền tố: Thêm tiền tố `_` cho private field (ví dụ: `private Rigidbody _rb;`), tiền tố `I` cho Interface (ví dụ: `IDamageable`).
- **Memory & Performance Optimization (Rất quan trọng):**
  - **Tránh GC Alloc trong Update:** Tuyệt đối KHÔNG `new` object, mảng, hoặc gọi `GetComponent<T>()`, `Find()`, `Camera.main` bên trong các hàm vòng lặp (`Update`, `FixedUpdate`, `LateUpdate`). Cache toàn bộ references trong `Awake()` hoặc `Start()`.
  - **String Concatenation:** Tránh cộng chuỗi trong Update; sử dụng `StringBuilder` hoặc cache sẵn chuỗi nếu cần cập nhật UI thường xuyên.
  - **Physics:** Mọi thao tác tác động lực, di chuyển `Rigidbody` hoặc kiểm tra Raycast vật lý PHẢI thực hiện trong `FixedUpdate()`.
  - **Object Pooling:** Luôn gợi ý hoặc triển khai `Object Pool` cho các object xuất hiện/biến mất liên tục (đạn, hiệu ứng particle, enemy spawn).

## 3. Quy tắc làm việc với Unity API & Components
- **Attribute Usage:**
  - Dùng `[SerializeField] private` thay vì `public` cho các biến cần kéo thả trên Inspector để đảm bảo tính đóng gói (Encapsulation).
  - Dùng `[RequireComponent(typeof(...))]` khi script phụ thuộc bắt buộc vào một Component khác.
  - Dùng `[Header("...")]` và `[Tooltip("...")]` để phân loại và giải thích các biến kéo thả trên Inspector.
- **Null Checking:** Dùng `if (component != null)` hoặc `if (component)` thay cho null-conditional operator (`component?.DoSomething()`) đối với `UnityEngine.Object` để tránh bỏ sót kiểm tra Native Unity Object Destruction.

## 4. Nguyên tắc Xử lý Multiplayer & UI (Tùy chọn)
- **Multiplayer / Networking:** 
  - Đảm bảo phân định rõ ràng giữa State/Logic chạy trên Server/Host (Network State) và hiển thị Local Visual (Visual/Sound).
  - Tuân thủ cơ chế Network Authority và Networked Properties tương ứng với framework mạng đang dùng.
- **UI & Gameplay Logic Isolation:** Không viết trực tiếp logic gameplay vào UI Script. Tách biệt UI View (`MonoBehaviour`) và UI Controller/Presenter.

## 5. Quy trình Thực thi Nhiệm vụ của Agent (Task Execution)
- **Review & Context Check:** Trước khi sửa đổi hoặc viết script mới, đọc kỹ các script liên quan để hiểu cấu trúc hiện tại, tránh trùng lặp code hay phá vỡ các Singleton/Manager sẵn có.
- **Safety Diffs:** Không thay đổi các biến public có nguy cơ làm mất liên kết (missing reference) trên Unity Inspector trừ khi được yêu cầu rõ ràng.
- **Self-Testing:** Sau khi hoàn thành logic C#, luôn tự kiểm tra cú pháp, đảm bảo không bị thiếu namespace (`using UnityEngine;`, v.v.) và kiểm tra xem có phương thức nào bị deprecated hay không.
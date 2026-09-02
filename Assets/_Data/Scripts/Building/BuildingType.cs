/*
 * BuildingType.cs
 * Folder: Scripts/Building/
 * Người làm: DŨNG
 *
 * Enum định nghĩa toàn bộ loại công trình trong KHẨN HOANG
 * Dùng chung cho: BuildingData, BuildingCtrl, BuildingState, BuildingManager,
 *                 GhostBuilding, BuildingSystem, TestBuildingPlacement
 *
 * QUY TẮC:
 *   - None  → giá trị mặc định, dùng để validate – KHÔNG dùng làm loại thật
 *   - Thêm loại mới vào đúng nhóm, cập nhật BuildingSystem.GetGhostPrefab() cùng lúc
 */

public enum BuildingType
{
    // ── GIÁ TRỊ MẶC ĐỊNH ───────────────────────
    None = 0,           // Chưa thiết lập – dùng để validate trong BuildingManager

    // ── NHÀ Ở ──────────────────────────────────
    House,              // Nhà ở cơ bản của dân

    // ── SẢN XUẤT / THU THẬP ────────────────────
    WoodCutter,         // Trại Mộc – worker đi chặt cây
    StoneMine,          // Mỏ Đá   – worker khai thác đá
    Kitchen,            // Nhà Bếp – chế biến lương thực

    // ── LƯU TRỮ ────────────────────────────────
    FoodStorage,        // Kho Lúa – lưu trữ lương thực
    StoneStorage,       // Kho Đá  – lưu trữ đá
    Warehouse,          // Kho Tổng – chứa tài nguyên tổng hợp

    // ── PHÒNG THỦ ──────────────────────────────
    WatchTower,         // Tháp Canh
    ArcherTower,        // Tháp Cung
    Cannon,             // Pháo

    // ── QUÂN SỰ (NHÀLÍNH) ──────────────────────
    BarracksMelee,      // Doanh Trại Lính Cận Chiến
    BarracksArcher,     // Doanh Trại Lính Cung
    BarracksSpear,      // Doanh Trại Lính Giáo

    // ── TÀI NGUYÊN ─────────────────────────────
    Wood,
    Rice,
    Stone,

    // ── QUÂN SỰ (LOẠI HUẤN LUYỆN) ─────────────
    // Các giá trị này chỉ dùng cho hàng đợi/ô huấn luyện, không phải công trình.
    // Thêm ở cuối để giữ nguyên giá trị đã lưu của các loại cũ.
    TroopCrossbow,      // Lính Nỏ
    TroopCannon,        // Lính Pháo
}

using App.Core;

namespace App.Storage.Repositories;

/// <summary>
/// Repository interface cho quản lý các item trong quarantine
/// </summary>
public interface IQuarantineRepository
{
    /// <summary>
    /// Tạo một item trong quarantine mới
    /// </summary>
    /// <param name="item">QuarantineItem cần tạo</param>
    /// <returns>QuarantineItem với ID đã được gán</returns>
    Task<QuarantineItem> CreateAsync(QuarantineItem item);

    /// <summary>
    /// Lấy item trong quarantine theo ID
    /// </summary>
    /// <param name="id">ID của item</param>
    /// <returns>QuarantineItem nếu tìm thấy, null nếu không</returns>
    Task<QuarantineItem?> GetByIdAsync(int id);

    /// <summary>
    /// Lấy tất cả item đang trong trạng thái Active
    /// </summary>
    /// <returns>Danh sách item đang active trong quarantine</returns>
    Task<List<QuarantineItem>> GetActiveAsync();

    /// <summary>
    /// Lấy tất cả item đã hết hạn (Expired)
    /// </summary>
    /// <returns>Danh sách item đã hết hạn</returns>
    Task<List<QuarantineItem>> GetExpiredAsync();

    /// <summary>
    /// Lấy item theo trạng thái
    /// </summary>
    /// <param name="status">Trạng thái cần lọc (Active, Restored, Deleted, Expired)</param>
    /// <returns>Danh sách item với trạng thái tương ứng</returns>
    Task<List<QuarantineItem>> GetByStatusAsync(QuarantineStatus status);

    /// <summary>
    /// Cập nhật trạng thái của một item
    /// </summary>
    /// <param name="id">ID của item</param>
    /// <param name="status">Trạng thái mới</param>
    /// <returns>true nếu cập nhật thành công, false nếu không</returns>
    Task<bool> UpdateStatusAsync(int id, QuarantineStatus status);

    /// <summary>
    /// Khôi phục một item từ quarantine về vị trí ban đầu
    /// </summary>
    /// <param name="id">ID của item cần khôi phục</param>
    /// <returns>true nếu khôi phục thành công, false nếu không</returns>
    Task<bool> RestoreAsync(int id);

    /// <summary>
    /// Xóa vĩnh viễn một item khỏi quarantine
    /// </summary>
    /// <param name="id">ID của item cần xóa</param>
    /// <returns>true nếu xóa thành công, false nếu không</returns>
    Task<bool> DeleteAsync(int id);
}

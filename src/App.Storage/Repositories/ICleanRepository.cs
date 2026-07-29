using App.Core;

namespace App.Storage.Repositories;

/// <summary>
/// Repository interface cho quản lý lịch sử dọn dẹp
/// </summary>
public interface ICleanRepository
{
    /// <summary>
    /// Tạo bản ghi lịch sử dọn dẹp mới
    /// </summary>
    /// <param name="history">CleanHistory cần tạo</param>
    /// <returns>CleanHistory với ID đã được gán</returns>
    Task<CleanHistory> CreateAsync(CleanHistory history);

    /// <summary>
    /// Lấy bản ghi lịch sử dọn dẹp theo ID
    /// </summary>
    /// <param name="id">ID của bản ghi</param>
    /// <returns>CleanHistory nếu tìm thấy, null nếu không</returns>
    Task<CleanHistory?> GetByIdAsync(int id);

    /// <summary>
    /// Lấy danh sách lịch sử dọn dẹp trong khoảng thời gian
    /// </summary>
    /// <param name="startDate">Ngày bắt đầu</param>
    /// <param name="endDate">Ngày kết thúc</param>
    /// <returns>Danh sách lịch sử dọn dẹp</returns>
    Task<List<CleanHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Lấy danh sách lịch sử dọn dẹp gần đây
    /// </summary>
    /// <param name="days">Số ngày trở lại (mặc định 30)</param>
    /// <returns>Danh sách lịch sử dọn dẹp mới nhất</returns>
    Task<List<CleanHistory>> GetRecentAsync(int days = 30);

    /// <summary>
    /// Lấy tổng dung lượng đã giải phóng (tính bằng bytes)
    /// </summary>
    /// <returns>Tổng dung lượng đã giải phóng</returns>
    Task<long> GetTotalFreedAsync();

    /// <summary>
    /// Lấy tổng số lần dọn dẹp đã thực hiện
    /// </summary>
    /// <returns>Tổng số lần dọn dẹp</returns>
    Task<int> GetTotalCleansAsync();
}

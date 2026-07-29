using App.Core;

namespace App.Storage.Repositories;

/// <summary>
/// Repository interface cho quản lý snapshot hiệu năng hệ thống
/// </summary>
public interface IPerformanceRepository
{
    /// <summary>
    /// Tạo một snapshot hiệu năng mới
    /// </summary>
    /// <param name="snapshot">PerformanceSnapshot cần tạo</param>
    /// <returns>PerformanceSnapshot với ID đã được gán</returns>
    Task<PerformanceSnapshot> CreateAsync(PerformanceSnapshot snapshot);

    /// <summary>
    /// Lấy snapshot hiệu năng mới nhất
    /// </summary>
    /// <returns>PerformanceSnapshot mới nhất, null nếu không có</returns>
    Task<PerformanceSnapshot?> GetLatestAsync();

    /// <summary>
    /// Lấy danh sách snapshot trong khoảng thời gian
    /// </summary>
    /// <param name="startDate">Ngày bắt đầu</param>
    /// <param name="endDate">Ngày kết thúc</param>
    /// <returns>Danh sách snapshot hiệu năng</returns>
    Task<List<PerformanceSnapshot>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Lấy danh sách snapshot gần đây
    /// </summary>
    /// <param name="minutes">Số phút trở lại (mặc định 60)</param>
    /// <returns>Danh sách snapshot hiệu năng mới nhất</returns>
    Task<List<PerformanceSnapshot>> GetRecentAsync(int minutes = 60);

    /// <summary>
    /// Lấy danh sách snapshot theo drive letter trong khoảng thời gian
    /// </summary>
    /// <param name="driveLetter">Ký tự drive (C, D, E...)</param>
    /// <param name="days">Số ngày trở lại (mặc định 7)</param>
    /// <returns>Danh sách snapshot hiệu năng của drive</returns>
    Task<List<PerformanceSnapshot>> GetByDriveAsync(string driveLetter, int days = 7);

    /// <summary>
    /// Lấy thống kê tổng hợp từ các snapshot
    /// </summary>
    /// <returns>AppStatistics với các thống kê từ snapshot</returns>
    Task<AppStatistics> GetStatisticsAsync();
}

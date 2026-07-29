using App.Core;

namespace App.Storage.Repositories;

/// <summary>
/// Repository interface cho quản lý phiên quét
/// </summary>
public interface IScanRepository
{
    /// <summary>
    /// Tạo một phiên quét mới
    /// </summary>
    /// <param name="session">ScanSession cần tạo</param>
    /// <returns>ScanSession với ID đã được gán</returns>
    Task<ScanSession> CreateAsync(ScanSession session);

    /// <summary>
    /// Lấy phiên quét theo ID
    /// </summary>
    /// <param name="id">ID của phiên quét</param>
    /// <returns>ScanSession nếu tìm thấy, null nếu không</returns>
    Task<ScanSession?> GetByIdAsync(int id);

    /// <summary>
    /// Lấy danh sách phiên quét theo loại trong khoảng thời gian
    /// </summary>
    /// <param name="type">Loại quét (Quick, Deep, Developer, Performance)</param>
    /// <param name="days">Số ngày trở lại (mặc định 30)</param>
    /// <returns>Danh sách phiên quét</returns>
    Task<List<ScanSession>> GetByTypeAsync(ScanType type, int days = 30);

    /// <summary>
    /// Lấy danh sách phiên quét gần đây
    /// </summary>
    /// <param name="days">Số ngày trở lại (mặc định 30)</param>
    /// <returns>Danh sách phiên quét mới nhất</returns>
    Task<List<ScanSession>> GetRecentAsync(int days = 30);

    /// <summary>
    /// Lấy tổng số phiên quét trong database
    /// </summary>
    /// <returns>Tổng số phiên quét</returns>
    Task<int> GetTotalCountAsync();
}

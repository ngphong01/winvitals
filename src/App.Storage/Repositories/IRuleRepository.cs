using App.Core;

namespace App.Storage.Repositories;

/// <summary>
/// Repository interface cho quản lý Rules trong Rule Engine
/// </summary>
public interface IRuleRepository
{
    /// <summary>
    /// Tạo một rule mới
    /// </summary>
    /// <param name="rule">Rule cần tạo</param>
    /// <returns>Rule với ID đã được gán</returns>
    Task<Rule> CreateAsync(Rule rule);

    /// <summary>
    /// Lấy rule theo ID
    /// </summary>
    /// <param name="id">ID của rule</param>
    /// <returns>Rule nếu tìm thấy, null nếu không</returns>
    Task<Rule?> GetByIdAsync(string id);

    /// <summary>
    /// Lấy tất cả các rule
    /// </summary>
    /// <returns>Danh sách tất cả rule</returns>
    Task<List<Rule>> GetAllAsync();

    /// <summary>
    /// Lấy các rule theo mức độ làm sạch
    /// </summary>
    /// <param name="level">Mức độ làm sạch (Quick, Deep, Developer, Custom)</param>
    /// <returns>Danh sách rule thuộc mức độ</returns>
    Task<List<Rule>> GetByLevelAsync(CleanLevel level);

    /// <summary>
    /// Cập nhật một rule
    /// </summary>
    /// <param name="rule">Rule với dữ liệu đã cập nhật</param>
    /// <returns>true nếu cập nhật thành công, false nếu không</returns>
    Task<bool> UpdateAsync(Rule rule);

    /// <summary>
    /// Xóa một rule
    /// </summary>
    /// <param name="id">ID của rule cần xóa</param>
    /// <returns>true nếu xóa thành công, false nếu không</returns>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// Bật/tắt một rule
    /// </summary>
    /// <param name="id">ID của rule cần bật/tắt</param>
    /// <returns>true nếu thành công, false nếu không</returns>
    Task<bool> ToggleAsync(string id);
}

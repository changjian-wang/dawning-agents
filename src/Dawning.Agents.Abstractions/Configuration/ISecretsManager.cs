namespace Dawning.Agents.Abstractions.Configuration;

/// <summary>
/// 密钥管理接口
/// </summary>
public interface ISecretsManager
{
    /// <summary>
    /// 获取密钥
    /// </summary>
    /// <param name="name">密钥名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>密钥值，如果不存在则返回 null</returns>
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置密钥
    /// </summary>
    /// <param name="name">密钥名称</param>
    /// <param name="value">密钥值</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除密钥
    /// </summary>
    /// <param name="name">密钥名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查密钥是否存在
    /// </summary>
    /// <param name="name">密钥名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
}

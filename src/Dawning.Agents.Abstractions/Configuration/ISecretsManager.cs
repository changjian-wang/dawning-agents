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

/// <summary>
/// 配置提供者接口
/// </summary>
public interface IConfigurationProvider
{
    /// <summary>
    /// 获取配置值
    /// </summary>
    /// <param name="key">配置键</param>
    /// <returns>配置值，如果不存在则返回 null</returns>
    string? GetValue(string key);

    /// <summary>
    /// 获取配置段
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="sectionName">段名称</param>
    /// <returns>配置实例</returns>
    T? GetSection<T>(string sectionName) where T : class, new();

    /// <summary>
    /// 重新加载配置
    /// </summary>
    void Reload();
}

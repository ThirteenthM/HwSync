namespace HwSync.Api.Contracts
{
    /// <summary>Ответ о готовности сервера.</summary>
    public sealed record HealthResponse(string Status);
}

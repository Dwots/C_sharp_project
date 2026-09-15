using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameLibApi.Services.Sharding;

namespace GameLibApi.Controllers;

/// <summary>
/// Управление шардированием (лабораторная №5).
/// </summary>
[ApiController]
[Route("api/sharding")]
[Tags("Sharding")]
public class ShardingController : ControllerBase
{
    private readonly ShardingService _sharding;

    public ShardingController(ShardingService sharding)
    {
        _sharding = sharding;
    }

    /// <summary>
    /// Часть 3: на какой шард router отправит запись этого пользователя.
    /// </summary>
    /// <param name="userId">Ключ шардирования.</param>
    /// <param name="strategy">modulo или consistent.</param>
    [HttpGet("route/{userId:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public ActionResult Route(int userId, [FromQuery] string strategy = "modulo")
    {
        var router = _sharding.CreateRouter(strategy, _sharding.ShardCount);

        return Ok(new
        {
            userId,
            strategy = router.Strategy,
            shardCount = router.ShardCount,
            shard = router.GetShard(userId)
        });
    }

    /// <summary>
    /// Часть 4: разложить реальные сессии из основной базы по шардам.
    /// </summary>
    [HttpPost("distribute")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Distribute(
        [FromQuery] string strategy = "modulo",
        [FromQuery] int limit = 100000)
    {
        return Ok(await _sharding.DistributeAsync(strategy, limit));
    }

    /// <summary>
    /// Часть 4: сколько записей реально лежит на каждом шарде.
    /// </summary>
    [HttpGet("distribution")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult> Distribution()
    {
        return Ok(await _sharding.GetDistributionAsync());
    }

    /// <summary>
    /// Части 5 и 7: сколько записей сменит шард при изменении их количества.
    /// </summary>
    [HttpGet("compare")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult> Compare(
        [FromQuery] int from = 3,
        [FromQuery] int to = 4,
        [FromQuery] int limit = 100000)
    {
        var modulo = await _sharding.CompareAsync("modulo", from, to, limit);
        var consistent = await _sharding.CompareAsync("consistent", from, to, limit);

        return Ok(new { modulo, consistent });
    }
}

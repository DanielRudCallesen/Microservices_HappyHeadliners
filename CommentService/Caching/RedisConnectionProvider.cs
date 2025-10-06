using StackExchange.Redis;

namespace CommentService.Caching
{
    public interface IRedisConnectionProvider
    {
        Task<IDatabase> GetDatabase();
    }
    public sealed class RedisConnectionProvider : IRedisConnectionProvider, IDisposable
    {
        private readonly string _connString;
        private ConnectionMultiplexer? _mux;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ILogger<RedisConnectionProvider> _logger;

        public RedisConnectionProvider(IConfiguration config, ILogger<RedisConnectionProvider> logger)
        {
            _connString = config.GetValue<string>("Redis:ConnectionString") ?? "redis:6379,abortConnect=false";
            _logger = logger;
        }

        public async Task<IDatabase> GetDatabase()
        {
            if (_mux is { IsConnected: true }) return _mux.GetDatabase();
            await _lock.WaitAsync();
            try
            {
                if (_mux is { IsConnected: true }) return _mux.GetDatabase();
                _mux = await ConnectionMultiplexer.ConnectAsync(_connString);
                _logger.LogInformation("Connected to Redis at {Conn}", _connString);
                return _mux.GetDatabase();
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Dispose()
        {
            try { _mux?.Dispose(); } catch { }
            _lock.Dispose();
        }
    }
}

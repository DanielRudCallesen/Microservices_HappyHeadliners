using StackExchange.Redis;

namespace ArticleService.Caching;
    public interface IRedisConnectionProvider
    {
        Task<IDatabase> GetDatabaseAsync();
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

        public async Task<IDatabase> GetDatabaseAsync()
        {
            if (_mux is { IsConnected: true }) return _mux.GetDatabase();

            await _lock.WaitAsync();
            try
            {
                if (_mux is { IsConnected: true }) return _mux.GetDatabase();
                _logger.LogInformation("Connecting to Redis at {Conn}", _connString);
                _mux = await ConnectionMultiplexer.ConnectAsync(_connString);
                _logger.LogInformation("Connected to Redis.");
                return _mux.GetDatabase();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Redis using '{Conn}'", _connString);
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Dispose()
        {
            _mux?.Dispose();
            _lock.Dispose();
        }
    }


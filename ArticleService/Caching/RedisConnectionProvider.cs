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

        public RedisConnectionProvider(IConfiguration config)
        {
            _connString = config.GetValue<string>("Redis:ConnectionString") ?? "redis:6379,abortConnect=false";
        }

        public async Task<IDatabase> GetDatabaseAsync()
        {
            if (_mux is { IsConnected: true }) return _mux.GetDatabase();

            await _lock.WaitAsync();
            try
            {
                if (_mux is { IsConnected: true }) return _mux.GetDatabase();
                _mux = await ConnectionMultiplexer.ConnectAsync(_connString);
                return _mux.GetDatabase();
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


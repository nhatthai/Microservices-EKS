
using Microsoft.Extensions.Options;

namespace NET6.WebAPI
{
    public class PathBaseStartupFilter : IStartupFilter
    {
        private readonly string _pathBase;

        // Takes an IOptions<PathBaseSettings> instead of a string directly
        public PathBaseStartupFilter(IOptions<PathBaseSettings> options)
        {
            _pathBase = options.Value.ApplicationPathBase;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UsePathBase(_pathBase);
                next(app);
            };
        }
    }

}
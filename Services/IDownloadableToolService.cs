using System;
using System.Threading.Tasks;

namespace MortysDLP.Services
{
    internal interface IDownloadableToolService
    {
        Task DownloadAssetAsync(string url, string targetPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    }
}

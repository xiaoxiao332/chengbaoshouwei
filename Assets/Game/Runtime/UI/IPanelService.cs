using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;

namespace FortressFrontier.Runtime.UI
{
    public interface IPanelService
    {
        Task OpenAsync(PanelKey id, object arguments, CancellationToken cancellationToken);
        Task<TView> OpenViewAsync<TView>(PanelKey id, object arguments, CancellationToken cancellationToken)
            where TView : class;
        Task CloseAsync(PanelKey id, CancellationToken cancellationToken);
    }
}

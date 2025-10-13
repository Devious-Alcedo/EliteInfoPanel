using System;

namespace EliteInfoPanel.Core
{
    public partial class GameStateService
    {
        // Support batch property change notifications
        private class UpdateScope : IDisposable
        {
            private readonly GameStateService _service;
            private bool _disposed;

            public UpdateScope(GameStateService service)
            {
                _service = service;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _service._isUpdating = false;
                    _service.SendPendingNotifications();
                    _disposed = true;
                }
            }
        }
    }
}

using System.Threading;

namespace DroneDDW.Neighborhood.Utilities
{
    /// <summary>
    /// A cancellation token source that lives as long as its component: created on first use, cancelled
    /// and disposed when the component is destroyed.
    /// </summary>
    internal sealed class NeighborhoodLifetime
    {
        #region Fields

        private CancellationTokenSource _source;
        private bool _isEnded;

        #endregion

        #region Properties

        /// <summary>The component's token; already cancelled once the component is gone.</summary>
        internal CancellationToken Token
        {
            get
            {
                if (_isEnded)
                {
                    return new CancellationToken(true);
                }

                if (_source == null)
                {
                    _source = new CancellationTokenSource();
                }

                return _source.Token;
            }
        }

        #endregion

        #region Internal

        internal void End()
        {
            _isEnded = true;
            if (_source == null)
            {
                return;
            }

            _source.Cancel();
            _source.Dispose();
            _source = null;
        }

        #endregion
    }
}

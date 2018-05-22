using System;

namespace g3
{
    /// <summary>
    /// interface that provides a cancel function
    /// </summary>
    public interface ICancelSource
    {
        bool Cancelled();
    }


    /// <summary>
    /// Just wraps a func<bool> as an ICancelSource
    /// </summary>
    public class CancelFunction : ICancelSource
    {
        public Func<bool> CancelF;
        public CancelFunction(Func<bool> cancelF) {
            CancelF = cancelF;
        }
        public bool Cancelled() { return CancelF(); }
    }


    /// <summary>
    /// This class is intended to be passed to long-running computes to 
    ///  1) provide progress info back to caller (not implemented yet)
    ///  2) allow caller to cancel the computation
    /// </summary>
    public class ProgressCancel
    {
        public ICancelSource Source;

        bool WasCancelled = false;  // will be set to true if CancelF() ever returns true

        public ProgressCancel(ICancelSource source)
        {
            Source = source;
        }
        public ProgressCancel(Func<bool> cancelF)
        {
            Source = new CancelFunction(cancelF);
        }

        /// <summary>
        /// Check if client would like to cancel
        /// </summary>
        public bool Cancelled()
        {
            if (WasCancelled)
                return true;
            WasCancelled = Source.Cancelled();
            return WasCancelled;
        }
    }
}

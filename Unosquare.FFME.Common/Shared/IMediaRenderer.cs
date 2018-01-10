namespace Unosquare.FFME.Shared
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a unified API for media rendering classes
    /// </summary>
    public interface IMediaRenderer
    {
        /// <summary>
        /// Gets the parent media engine.
        /// </summary>
        MediaEngine MediaCore { get; }

        /// <summary>
        /// Waits for the renderer to be ready to render.
        /// </summary>
        /// <returns>The awaitable task</returns>
        Task WaitForReadyState();

        /// <summary>
        /// Executed when the Play method is called on the parent MediaElement
        /// </summary>
        /// <returns>The awaitable task</returns>
        Task Play();

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        /// <returns>The awaitable task</returns>
        Task Pause();

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        /// <returns>The awaitable task</returns>
        Task Stop();

        /// <summary>
        /// Executed when the Close method is called on the parent MediaElement
        /// </summary>
        /// <returns>The awaitable task</returns>
        Task Close();

        /// <summary>
        /// Executed after a Seek operation is performed on the parent MediaElement
        /// </summary>
        /// <returns>The awaitable task</returns>
        Task Seek();

        /// <summary>
        /// Called when a media block is due rendering.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <returns>The awaitable task</returns>
        Task Render(MediaBlock mediaBlock, TimeSpan clockPosition);

        /// <summary>
        /// Called on every block rendering clock cycle just in case some update operation needs to be performed.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="clockPosition">The clock position.</param>
        /// <returns>The awaitable task</returns>
        Task Update(TimeSpan clockPosition);
    }
}

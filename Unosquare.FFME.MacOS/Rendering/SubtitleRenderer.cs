namespace Unosquare.FFME.MacOS.Rendering
{
    using System;
    using System.Threading.Tasks;
    using Unosquare.FFME.Shared;

    /// <summary>
    /// Subtitle Renderer - Does nothing at this point.
    /// </summary>
    internal class SubtitleRenderer : IMediaRenderer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Unosquare.FFME.MacOS.Rendering.SubtitleRenderer"/> class.
        /// </summary>
        /// <param name="mediaEngine">Media element core.</param>
        public SubtitleRenderer(MediaEngine mediaEngine)
        {
            MediaCore = mediaEngine;
        }

        /// <summary>
        /// Gets the media element core player component.
        /// </summary>
        /// <value>The media element core.</value>
        public MediaEngine MediaCore { get; }

        public async Task Close()
        {
            await Task.CompletedTask;
        }

        public async Task Pause()
        {
            await Task.CompletedTask;
        }

        public async Task Play()
        {
            await Task.CompletedTask;
        }

        public async Task Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            await Task.CompletedTask;
        }

        public async Task Seek()
        {
            await Task.CompletedTask;
        }

        public async Task Stop()
        {
            await Task.CompletedTask;
        }

        public async Task Update(TimeSpan clockPosition)
        {
            await Task.CompletedTask;
        }

        public async Task WaitForReadyState()
        {
            await Task.CompletedTask;
        }
    }
}

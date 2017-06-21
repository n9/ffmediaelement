﻿namespace Unosquare.FFME.Rendering
{
    using Core;
    using Decoding;
    using Rendering.Wave;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Threading;

    /// <summary>
    /// Provides Audio Output capabilities by writing samples to the default audio output device.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Rendering.Wave.IWaveProvider" />
    /// <seealso cref="Unosquare.FFME.Rendering.IRenderer" />
    /// <seealso cref="System.IDisposable" />
    internal sealed class AudioRenderer : IDisposable, IRenderer, IWaveProvider
    {
        #region Private Members

        private readonly MediaElement MediaElement;
        private WavePlayer AudioDevice;
        private CircularBuffer AudioBuffer;
        private bool IsDisposed = false;

        private byte[] SilenceBuffer = null;
        private byte[] ReadBuffer = null;
        private double LeftVolume = 1.0d;
        private double RightVolume = 1.0d;

        private WaveFormat m_Format = null;
        private double m_Volume = 1.0d;
        private double m_Balance = 0.0d;
        private volatile bool m_IsMuted = false;

        private int BytesPerSample = 2;
        private double SyncThesholdMilliseconds = 0d;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioRenderer"/> class.
        /// </summary>
        /// <param name="mediaElement">The media element.</param>
        public AudioRenderer(MediaElement mediaElement)
        {
            MediaElement = mediaElement;

            m_Format = new WaveFormat(AudioParams.Output.SampleRate, AudioParams.OutputBitsPerSample, AudioParams.Output.ChannelCount);
            if (WaveFormat.BitsPerSample != 16 || WaveFormat.Channels != 2)
                throw new NotSupportedException("Wave Format has to be 16-bit and 2-channel.");

            BytesPerSample = WaveFormat.BitsPerSample / 8;
            SilenceBuffer = new byte[m_Format.BitsPerSample / 8 * m_Format.Channels * 2];

            if (MediaElement.HasAudio)
                Initialize();

            if (Application.Current != null)
                MediaElement.InvokeOnUI(DispatcherPriority.Normal, () =>
                {
                    Application.Current.Exit += OnApplicationExit;
                });
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            try { Dispose(); }
            catch { }
        }

        #endregion

        #region Initialization and Destruction

        /// <summary>
        /// Initializes the audio renderer.
        /// Call the Play Method to start reading samples
        /// </summary>
        private void Initialize()
        {
            Destroy();

            AudioDevice = new WavePlayer()
            {
                DesiredLatency = 200,
                NumberOfBuffers = 2,
            };

            SyncThesholdMilliseconds = 0.05 * DesiredLatency.TotalMilliseconds; // ~5% sync threshold for audio samples 
            var bufferLength = WaveFormat.ConvertLatencyToByteSize(AudioDevice.DesiredLatency) * MediaElement.Blocks[MediaType.Audio].Capacity / 2;
            AudioBuffer = new CircularBuffer(bufferLength);
            AudioDevice.Init(this);
        }


        /// <summary>
        /// Destroys the audio renderer.
        /// Makes it useless.
        /// </summary>
        private void Destroy()
        {
            try
            {
                // Remove the event handler
                if (Application.Current != null)
                    MediaElement.InvokeOnUI(DispatcherPriority.Normal, () =>
                    {
                        Application.Current.Exit -= OnApplicationExit;
                    });
            }
            catch { }

            if (AudioDevice != null)
            {
                AudioDevice.Stop();
                AudioDevice.Dispose();
                AudioDevice = null;
            }

            if (AudioBuffer != null)
            {
                AudioBuffer.Dispose();
                AudioBuffer = null;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the output format of the audio
        /// </summary>
        public WaveFormat WaveFormat
        {
            get { return m_Format; }
        }


        /// <summary>
        /// Gets or sets the volume.
        /// </summary>
        /// <value>
        /// The volume.
        /// </value>
        public double Volume
        {
            get { return Thread.VolatileRead(ref m_Volume); }
            set
            {
                if (value < 0) value = 0;
                if (value > 1) value = 1;

                var leftFactor = m_Balance > 0 ? 1d - m_Balance : 1d;
                var rightFactor = m_Balance < 0 ? 1d + m_Balance : 1d;

                LeftVolume = leftFactor * value;
                RightVolume = rightFactor * value;
                Thread.VolatileWrite(ref m_Volume, value);
            }
        }

        /// <summary>
        /// Gets or sets the balance (-1.0 to 1.0).
        /// </summary>
        public double Balance
        {
            get { return Thread.VolatileRead(ref m_Balance); }
            set
            {
                if (value < -1.0) value = -1.0;
                if (value > 1.0) value = 1.0;
                Thread.VolatileWrite(ref m_Balance, value);
                Volume = Thread.VolatileRead(ref m_Volume);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the wave output is muted.
        /// </summary>
        public bool IsMuted
        {
            get { return m_IsMuted; }
            set { m_IsMuted = value; }
        }


        /// <summary>
        /// Gets the realtime latency of the audio relative to the internal wall clock.
        /// A negative value means audio is ahead of the wall clock.
        /// A positive value means audio is behind of the wall clock.
        /// </summary>
        public TimeSpan Latency
        {
            get
            {
                // the pending audio length is the amount of audio samples time that has not been yet read by the audio device.
                var pendingAudioLength = TimeSpan.FromTicks(
                    (long)Math.Round(TimeSpan.TicksPerMillisecond * 1000d * AudioBuffer.ReadableCount / WaveFormat.AverageBytesPerSecond, 0));

                // If we don't have a valid write tag our best best is the readable pending audio
                if (AudioBuffer.WriteTag == TimeSpan.MinValue)
                    return pendingAudioLength;

                // The write tage is the last PTS written to the audio buffer, therefore, the current position
                // is just what has been written minus the what has not been read (whatever is pending)
                // The above is not necessarily 100% true. As the samples that have already been picked up
                // by the sound card might not have finished playing. But that is why we read this property only when
                // the sound card wants the next batch of samples!
                var currentPosition = TimeSpan.FromTicks(AudioBuffer.WriteTag.Ticks - pendingAudioLength.Ticks);

                // Finally the delay is the clock position minus the current position
                return TimeSpan.FromTicks(MediaElement.Clock.Position.Ticks - currentPosition.Ticks);
            }
        }

        /// <summary>
        /// Gets the desired latency odf the audio device.
        /// Value is always positive and typically 200ms. This means audio gets rendered up to this late behind the wall clock.
        /// </summary>
        public TimeSpan DesiredLatency
        {
            get { return TimeSpan.FromTicks((AudioDevice?.DesiredLatency ?? 1) * TimeSpan.TicksPerMillisecond); }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Renders the specified media block.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <param name="renderIndex">Index of the render.</param>
        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition, int renderIndex)
        {
            if (AudioBuffer == null) return;
            var block = mediaBlock as AudioBlock;
            if (block == null) return;

            var currentIndex = renderIndex;
            var audioBlocks = MediaElement.Blocks[MediaType.Audio];
            var addedBlockCount = 0;
            var addedBytes = 0;
            while (currentIndex >= 0 && currentIndex < audioBlocks.Count)
            {
                var audioBlock = audioBlocks[currentIndex] as AudioBlock;
                if (AudioBuffer.WriteTag < audioBlock.StartTime)
                {
                    AudioBuffer.Write(audioBlock.Buffer, audioBlock.BufferLength, audioBlock.StartTime, true);
                    addedBlockCount++;
                    addedBytes += audioBlock.BufferLength;
                }

                currentIndex++;

                // Stop adding if we have too much in there.
                if (AudioBuffer.CapacityPercent >= 0.8)
                    break;
            }
        }

        /// <summary>
        /// Executed when the Play method is called on the parent MediaElement
        /// </summary>
        public void Play()
        {
            AudioDevice?.Play();
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        public void Pause()
        {
            //AudioDevice?.Pause();
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        public void Stop()
        {
            //AudioDevice?.Stop();
            AudioBuffer.Clear();
        }

        /// <summary>
        /// Executed when the Close method is called on the parent MediaElement
        /// </summary>
        public void Close()
        {
            Destroy();
        }

        /// <summary>
        /// Executed after a Seek operation is performed on the parent MediaElement
        /// </summary>
        public void Seek()
        {
            AudioBuffer.Clear();
        }

        #endregion

        #region IWaveProvider Support

        /// <summary>
        /// Synchronizes audio rendering to the wall clock.
        /// Returns true if additional samples need to be read.
        /// Returns false if silence has been written and no further reading is required.
        /// </summary>
        /// <param name="targetBuffer">The target buffer.</param>
        /// <param name="targetBufferOffset">The target buffer offset.</param>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Synchronize(byte[] targetBuffer, int targetBufferOffset, int requestedBytes)
        {
            var audioLatency = Latency;

            if (audioLatency.TotalMilliseconds > SyncThesholdMilliseconds)
            {
                // a positive audio latency means we are rendering audio behind (after) the clock (skip some samples)
                // and therefore we need to advance the buffer before we read from it.
                MediaElement.Container.Log(MediaLogMessageType.Warning, $"Audio Sync (SKIP): {audioLatency.TotalMilliseconds,8:0.00}ms | Audio samples are behind of clock.");

                // skip some samples from the buffer.
                var audioLatencyBytes = WaveFormat.ConvertLatencyToByteSize((int)Math.Ceiling(audioLatency.TotalMilliseconds));
                AudioBuffer.Skip(Math.Min(audioLatencyBytes, AudioBuffer.ReadableCount));
            }
            else if (audioLatency.TotalMilliseconds < -SyncThesholdMilliseconds)
            {
                // a negative audio latency means we are rendering audio ahead (before) the clock
                // and therefore we need to render some silence until the clock catches up
                MediaElement.Container.Log(MediaLogMessageType.Warning, $"Audio Sync (WAIT): {audioLatency.TotalMilliseconds,8:0.00}ms | Audio samples are ahead of clock.");

                for (var i = targetBufferOffset; i < targetBufferOffset + requestedBytes; i++)
                    targetBuffer[i] = 0;

                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Stretch(byte[] targetBuffer, int targetBufferOffset, int requestedBytes)
        {
            var blockSize = BytesPerSample * WaveFormat.Channels;
            var bytesToRead = Math.Min(
                AudioBuffer.ReadableCount,
                (int)(requestedBytes * MediaElement.Clock.SpeedRatio).ToMultipleOf(blockSize));

            var bytesToAdd = requestedBytes - bytesToRead;
            var sourceOffset = requestedBytes;
            AudioBuffer.Read(bytesToRead, ReadBuffer, sourceOffset);

            var targetOffset = 0;
            var stepSize = ((double)requestedBytes / bytesToAdd); // repeat block every x blocks
            var stepAccum = 0d;
            while (targetOffset < requestedBytes)
            {
                if (stepAccum >= stepSize)
                {
                    targetOffset += blockSize;
                    for (var t = 0; t < blockSize; t++)
                        ReadBuffer[targetOffset + t] = ReadBuffer[sourceOffset + t];

                    stepAccum = stepAccum - stepSize;
                    stepAccum = stepAccum - (int)stepAccum;
                }

                for (var t = 0; t < blockSize; t++)
                    ReadBuffer[targetOffset + t] = ReadBuffer[sourceOffset + t];

                sourceOffset += blockSize;
                targetOffset += blockSize;
                stepAccum += 1d;
            }

        }

        /// <summary>
        /// Applies volume and balance to the audio samples and writes them
        /// to the specified target buffer.
        /// </summary>
        /// <param name="targetBuffer">The target buffer.</param>
        /// <param name="targetBufferOffset">The target buffer offset.</param>
        /// <param name="requestedBytes">The requested bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyVolumeAndBalance(byte[] targetBuffer, int targetBufferOffset, int requestedBytes)
        {
            // Samples are interleaved (left and right in 16-bit signed integers each)
            var isLeftSample = true;
            for (var sourceBufferOffset = 0; sourceBufferOffset < requestedBytes; sourceBufferOffset += BytesPerSample)
            {
                // The sample has 2 bytes: at the base index is the LSB and at the baseIndex + 1 is the MSB
                // this obviously only holds true for Little Endian architectures, and thus, the current code is not portable.
                // This replaces BitConverter.ToInt16(ReadBuffer, baseIndex); which is obviously much slower.
                var sample = (short)(ReadBuffer[sourceBufferOffset] | (ReadBuffer[sourceBufferOffset + 1] << 8));

                if (IsMuted)
                {
                    sample = 0;
                }
                else
                {
                    if (isLeftSample && LeftVolume != 1.0)
                        sample = (short)(sample * LeftVolume);
                    else if (isLeftSample == false && RightVolume != 1.0)
                        sample = (short)(sample * RightVolume);
                }

                targetBuffer[targetBufferOffset + sourceBufferOffset] = (byte)(sample & 0xff);
                targetBuffer[targetBufferOffset + sourceBufferOffset + 1] = (byte)(sample >> 8);
                isLeftSample = !isLeftSample;
            }
        }

        /// <summary>
        /// Called whenever the audio driver requests samples.
        /// Do not call this method directly.
        /// </summary>
        /// <param name="targetBuffer">The render buffer.</param>
        /// <param name="targetBufferOffset">The render buffer offset.</param>
        /// <param name="requestedBytes">The requested bytes.</param>
        /// <returns></returns>
        public int Read(byte[] targetBuffer, int targetBufferOffset, int requestedBytes)
        {
            if (MediaElement.IsPlaying == false || MediaElement.HasAudio == false || AudioBuffer.ReadableCount <= 0)
            {
                Buffer.BlockCopy(SilenceBuffer, 0, targetBuffer, targetBufferOffset, Math.Min(SilenceBuffer.Length, targetBuffer.Length));
                return SilenceBuffer.Length;
            }

            if (ReadBuffer == null || ReadBuffer.Length < (int)(requestedBytes * Constants.MaxSpeedRatio))
                ReadBuffer = new byte[(int)(requestedBytes * Constants.MaxSpeedRatio)];

            if (MediaElement.HasVideo && Synchronize(targetBuffer, targetBufferOffset, requestedBytes) == false)
                return requestedBytes;


            if (MediaElement.Clock.SpeedRatio < 1.0)
            {
                Stretch(targetBuffer, targetBufferOffset, requestedBytes);
            }
            else if (MediaElement.Clock.SpeedRatio > 1.0)
            {
                // TODO: replace with Shorten
                requestedBytes = Math.Min(requestedBytes, AudioBuffer.ReadableCount);
                AudioBuffer.Read(requestedBytes, ReadBuffer, 0);
            }
            else
            {
                requestedBytes = Math.Min(requestedBytes, AudioBuffer.ReadableCount);
                AudioBuffer.Read(requestedBytes, ReadBuffer, 0);
            }

            ApplyVolumeAndBalance(targetBuffer, targetBufferOffset, requestedBytes);

            return requestedBytes;
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        ///   <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                    Destroy();

                IsDisposed = true;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

    }
}
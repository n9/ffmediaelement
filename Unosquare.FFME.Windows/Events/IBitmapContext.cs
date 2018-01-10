namespace Unosquare.FFME.Events
{
    using System;
    using System.Windows.Media.Imaging;

    public interface IBitmapContext : IDisposable
    {
        BitmapSource BitmapSource { get; }

        System.Drawing.Bitmap Overlay { get; }
    }
}

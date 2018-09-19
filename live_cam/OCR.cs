
using System;
using System.Drawing;
using System.Diagnostics;
using Tesseract;
using ScreenShotDemo;

namespace live_cam
{
    class OCR : DisposableBase
    {
        private readonly IntPtr ppWindowHandle;
        private ScreenCapture screenCapture;
        private TesseractEngine ocr;

        public OCR()
        {
            ppWindowHandle = GetHandle();
            screenCapture = new ScreenCapture();
            ocr = new TesseractEngine(@"../../tessdata", "eng", EngineMode.Default);
        }

        // Let tesseract read GetWindowScreenshot' bitmap and return text
        public string GetNextText()
        {
            try
            {
                using (Page page = ocr.Process(GetWindowScreenshot(), PageSegMode.Auto))
                    return page.GetText();
            }
            catch (Exception)
            {
                return "";
            }
        }

        // Return bitmap containing content of PPShow's window
        private Bitmap GetWindowScreenshot()
        {
            return new Bitmap(screenCapture.CaptureWindow(ppWindowHandle));
        }

        // Try finding the PPShow window and return its handle
        private IntPtr GetHandle()
        {
            IntPtr ppWindowHandle = IntPtr.Zero;
            foreach (Process proc in Process.GetProcesses())
            {
                if (proc.MainWindowTitle == "PPShow")
                    ppWindowHandle = proc.MainWindowHandle;
            }
            if (ppWindowHandle == IntPtr.Zero)
                throw new Exception("OCR could not find ppWindowHandle");
            return ppWindowHandle;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ocr.Dispose();
        }
    }
}

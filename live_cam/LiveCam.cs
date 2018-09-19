
using System;
using System.Timers;
using System.Collections.Generic;
using System.Windows.Forms;
using Utilities;
using Tesseract;
using System.Text.RegularExpressions;
using ScreenShotDemo;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace live_cam
{
    public partial class LiveCam : Form
    {

        private static bool DEBUG_WITH_PP = true;
        private static bool DEBUG_WITH_TAPPING = true;
        private static bool DEBUG_WITH_CURSOR = true;
        private static int updateRate = 50;

        private static KeyboardHook hook = new KeyboardHook();
        private static System.Timers.Timer timer;
        private static List<KeyboardHook.VKeys> pressedKeys = new List<KeyboardHook.VKeys>();
        private static int currSecX = 0;
        private static int currSecY = 0;
        private static int xSectors = 4;
        private static int ySectors = 2;
        private static Dictionary<int, Bitmap> imageMap = new Dictionary<int, Bitmap>();
        private static int width;
        private static int height;
        private static int lastImage;
        private static int lastTapping;
        private static Random rand;
        private IntPtr ppWindowHandle;
        //private TesseractEngine ocr;
        private ScreenCapture sc;
        //private int ppValue;
        private int lastPpValue;
        private int highestPp;
        private int mood;
        private int lastMood;
        private int ocrDelayer;
        private static OCR ocr;

        KeyboardHook.VKeys button1 = KeyboardHook.VKeys.NUMPAD4;
        KeyboardHook.VKeys button2 = KeyboardHook.VKeys.NUMPAD5;

        public LiveCam()
        {
            KeyPreview = true;

            InitializeVariables();
            InitializeComponent();
            SetTimer();

            ppWindowHandle = Handle;
            foreach (Process proc in Process.GetProcesses())
            {
                if (proc.MainWindowTitle == "PPShow")
                    ppWindowHandle = proc.MainWindowHandle;
            }
            if (ppWindowHandle == Handle)
                Console.WriteLine("no pp handle found");

            hook.KeyDown += Hook_KeyDown;
            hook.KeyUp += Hook_KeyUp;
            hook.Install();
            
        }

        private void Hook_KeyUp(KeyboardHook.VKeys key)
        {
            pressedKeys.Remove(key);
        }

        private void Hook_KeyDown(KeyboardHook.VKeys key)
        {
            if (!pressedKeys.Contains(key))
                pressedKeys.Add(key);
        }

        private int HashInts(int i1, int i2, int i3, int i4)
        {
            int hash = 23;
            hash = hash * 31 + i1;
            hash = hash * 31 + i2;
            hash = hash * 31 + i3;
            hash = hash * 31 + i4;
            return hash;
        }

        private void InitializeVariables()
        {
            width = Screen.FromControl(this).Bounds.Width;
            height = Screen.FromControl(this).Bounds.Height;

            highestPp = 0;
            lastPpValue = 0;
            lastImage = -1;     // Anything but a result from HashInts function
            lastTapping = 0;    // no tapping
            mood = 2;           // neutral

            rand = new Random();

            //ppValue = 0;
            sc = new ScreenCapture();
            ocrDelayer = 0;
            ocr = new OCR();

            CreateSprites();
        }

        private void SetTimer()
        {
            timer = new System.Timers.Timer(updateRate);
            timer.Elapsed += OnTimedEvent;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            ocrDelayer++;
            if (ocrDelayer == 50)
            {
                ocrDelayer = 0;
                int currPP = GetOcrInt();
                if (currPP != -1)
                {
                    lastPpValue = currPP;
                }
            }
            LoadNewPicture();
        }

        private int GetOcrInt()
        {
            string ocrResult = Regex.Replace(ocr.GetNextText().Split('.')[0], @"[^\d]", "").Replace("\n", "");
            if (int.TryParse(ocrResult, out int ppValue) && ppValue >= 0 && ppValue <= 2000)
                return ppValue;
            else
                return -1;
        }

        private void SetCurrentSector()
        {
            // Map cursor position to sector range
            currSecX = (int)((double)xSectors / width * MousePosition.X);
            currSecY = (int)((double)ySectors / height * MousePosition.Y);

            // If outside of main screen
            if (currSecX < 0 || currSecX > xSectors - 1 || currSecY < 0 || currSecY > ySectors - 1)
            {
                currSecX = 0;
                currSecY = 0;
            }
        }

        private int AnalyzeMood()
        {
            if (lastPpValue < highestPp * 0.95)
            {
                return 3;
            }
            else if (lastPpValue < highestPp * 0.85)
            {
                return 4;
            }

            if (lastPpValue == 0 || (lastPpValue >= 110 && lastPpValue < 170))
            {
                mood = 2;
            }
            else if (lastPpValue > 0 && lastPpValue < 60)
            {
                mood = 4;
            }
            else if (lastPpValue >= 60 && lastPpValue < 110)
            {
                mood = 3;
            }
            else if (lastPpValue >= 170 && lastPpValue < 220)
            {
                mood = 1;
            }
            else if (lastPpValue >= 220)
            {
                mood = 0;
            }
            return mood;
        }

        private int AnalyzeTapping()
        {
            // 0: no tapping --- 1: button1 --- 2: button2 --- 1 or 2 if other button
            int tapping = 0;
            if (pressedKeys.Count == 1)
            {
                if (pressedKeys[0] == button1)
                    tapping = 1;
                else if (pressedKeys[0] == button2)
                    tapping = 2;
                else
                    tapping = rand.Next(1, 3);
            }
            else if (pressedKeys.Count > 1)
                tapping = lastTapping;
            return tapping;
        }

        private void LoadNewPicture()
        {
            if (DEBUG_WITH_CURSOR)
                SetCurrentSector();
            if (DEBUG_WITH_TAPPING)
                lastTapping = AnalyzeTapping();
            if (DEBUG_WITH_PP)
                lastMood = AnalyzeMood();

            int mapKey = HashInts(currSecX, currSecY, lastTapping, lastMood);
            if (lastImage == mapKey)    // no unnecessary image retrieving
                return;
            lastImage = mapKey;
            pictureBox1.Image = imageMap[mapKey];
        }

        public Bitmap Superimpose(Bitmap largeBmp, Bitmap smallBmp, int x_margin, int y_margin)
        {
            Bitmap largeBmpCopy = (Bitmap)largeBmp.Clone();
            Bitmap smallBmpCopy = (Bitmap)smallBmp.Clone();
            Graphics g = Graphics.FromImage(largeBmpCopy);
            g.CompositingMode = CompositingMode.SourceOver;
            int x = largeBmpCopy.Width - smallBmpCopy.Width - x_margin;
            int y = largeBmpCopy.Height - smallBmpCopy.Height - y_margin;
            g.DrawImage(smallBmpCopy, new Point(x, y));
            return largeBmpCopy;
        }

        private void FillImageMap(Bitmap baseSprite, int mouth, int tap)
        {
            int cursor_x_margin = 190;
            int cursor_y_margin = 75;

            // x sector, y sector, tapping (no tapping, button1, button2), mood (very happy, happy, neutral, sad, very sad)
            imageMap.Add(HashInts(0, 0, tap, mouth), Superimpose(baseSprite, Properties.Resources.c00, cursor_x_margin, cursor_y_margin));
            imageMap.Add(HashInts(1, 0, tap, mouth), Superimpose(baseSprite, Properties.Resources.c10, cursor_x_margin, cursor_y_margin));
            imageMap.Add(HashInts(2, 0, tap, mouth), Superimpose(baseSprite, Properties.Resources.c20, cursor_x_margin, cursor_y_margin));
            imageMap.Add(HashInts(3, 0, tap, mouth), Superimpose(baseSprite, Properties.Resources.c30, cursor_x_margin, cursor_y_margin));
            imageMap.Add(HashInts(0, 1, tap, mouth), Superimpose(baseSprite, Properties.Resources.c01, cursor_x_margin, cursor_y_margin));
            imageMap.Add(HashInts(1, 1, tap, mouth), Superimpose(baseSprite, Properties.Resources.c11, cursor_x_margin, cursor_y_margin));
            imageMap.Add(HashInts(2, 1, tap, mouth), Superimpose(baseSprite, Properties.Resources.c21, cursor_x_margin, cursor_y_margin));
            imageMap.Add(HashInts(3, 1, tap, mouth), Superimpose(baseSprite, Properties.Resources.c31, cursor_x_margin, cursor_y_margin));
        }

        private void CreateSprites()
        {
            /*
            Bitmap bc01 = Superimpose(baseSprite, c01, 360 - 35 - 135, 270 - 100 - 115 + 20);
            Bitmap btU = Superimpose(baseSprite, tU, 360 - 160 - 130, 270 - 115 - 130 + 20);
            Bitmap bmS2 = Superimpose(baseSprite, mS2, 360 - 150 - 100, 270 - 90 - 50 + 20);
            */

            /*
            // Cursor arm:
            int xStart = 35;
            int yStart = 115;
            int pWidth = 135;
            int pHeight = 100;
            
            // Tapping arm:
            int xStart = 160;
            int yStart = 130;
            int pWidth = 130;
            int pHeight = 105;
            
            // Mouth:
            int xStart = 150;
            int yStart = 75;
            int pWidth = 100;
            int pHeight = 30;
            */

            int mouth_x_margin = 110;
            int mouth_y_margin = 165;
            int tap_x_margin = 70;
            int tap_y_margin = 55;


            Bitmap b = Properties.Resources.baseSprite;
            using (Bitmap happy2 = Superimpose(b, Properties.Resources.mH2, mouth_x_margin, mouth_y_margin))
            {
                using (Bitmap tapL = Superimpose(happy2, Properties.Resources.tL, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapL, 0, 2);
                }
                using (Bitmap tapR = Superimpose(happy2, Properties.Resources.tR, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapR, 0, 1);
                }
                using (Bitmap tapU = Superimpose(happy2, Properties.Resources.tU, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapU, 0, 0);
                }
            }
            using (Bitmap happy1 = Superimpose(b, Properties.Resources.mH1, mouth_x_margin, mouth_y_margin))
            {
                using (Bitmap tapL = Superimpose(happy1, Properties.Resources.tL, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapL, 1, 2);
                }
                using (Bitmap tapR = Superimpose(happy1, Properties.Resources.tR, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapR, 1, 1);
                }
                using (Bitmap tapU = Superimpose(happy1, Properties.Resources.tU, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapU, 1, 0);
                }
            }
            using (Bitmap neutral = Superimpose(b, Properties.Resources.mN, mouth_x_margin, mouth_y_margin))
            {
                using (Bitmap tapL = Superimpose(neutral, Properties.Resources.tL, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapL, 2, 2);
                }
                using (Bitmap tapR = Superimpose(neutral, Properties.Resources.tR, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapR, 2, 1);
                }
                using (Bitmap tapU = Superimpose(neutral, Properties.Resources.tU, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapU, 2, 0);
                }
            }
            using (Bitmap sad1 = Superimpose(b, Properties.Resources.mS1, mouth_x_margin, mouth_y_margin))
            {
                using (Bitmap tapL = Superimpose(sad1, Properties.Resources.tL, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapL, 3, 2);
                }
                using (Bitmap tapR = Superimpose(sad1, Properties.Resources.tR, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapR, 3, 1);
                }
                using (Bitmap tapU = Superimpose(sad1, Properties.Resources.tU, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapU, 3, 0);
                }
            }
            using (Bitmap sad2 = Superimpose(b, Properties.Resources.mS2, mouth_x_margin, mouth_y_margin))
            {
                using (Bitmap tapL = Superimpose(sad2, Properties.Resources.tL, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapL, 4, 2);
                }
                using (Bitmap tapR = Superimpose(sad2, Properties.Resources.tR, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapR, 4, 1);
                }
                using (Bitmap tapU = Superimpose(sad2, Properties.Resources.tU, tap_x_margin, tap_y_margin))
                {
                    FillImageMap(tapU, 4, 0);
                }
            }
        }
    }
}

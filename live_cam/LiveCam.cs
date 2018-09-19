
using System;
using System.Timers;
using System.Collections.Generic;
using System.Windows.Forms;
using Utilities;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace live_cam
{
    public partial class LiveCam : Form
    {
        private const bool D_WITH_PP = true;
        private const bool D_WITH_TAPPING = true;
        private const bool D_WITH_CURSOR = true;
        private const int updateRate = 50;
        private const int xSectors = 4;
        private const int ySectors = 2;
        private const KeyboardHook.VKeys button1 = KeyboardHook.VKeys.NUMPAD4;
        private const KeyboardHook.VKeys button2 = KeyboardHook.VKeys.NUMPAD5;

        private static KeyboardHook hook = new KeyboardHook();
        private static System.Timers.Timer pollTimer;
        private static List<KeyboardHook.VKeys> pressedKeys = new List<KeyboardHook.VKeys>();
        private static int lastPressedKeysHash = GetSequenceHashCode(pressedKeys);
        private static Dictionary<int, Bitmap> imageMap = new Dictionary<int, Bitmap>();
        private static int width;
        private static int height;
        private static int lastImage = -1;     // Anything but a result from HashInts function
        private static int tapping = 0;        // no tapping
        private static Random rand = new Random();
        private static int lastPpValue = 0;
        private static int highestPp = 0;
        private static int ocrDelayer = 0;
        private static OCR ocr;

        public LiveCam()
        {
            InitializeVariables();
            InitializeComponent();
            SetTimer();

            if (D_WITH_TAPPING)
            {
                hook.KeyDown += Hook_KeyDown;
                hook.KeyUp += Hook_KeyUp;
                hook.Install();
            }
        }

        // Called on hook's KeyUp event, remove key from pressedKeys
        private void Hook_KeyUp(KeyboardHook.VKeys key)
        {
            pressedKeys.Remove(key);
        }

        // Called on hook's KeyDown event, add key to pressedKeys
        private void Hook_KeyDown(KeyboardHook.VKeys key)
        {
            if (!pressedKeys.Contains(key))
                pressedKeys.Add(key);
        }

        // Variable initializing
        private void InitializeVariables()
        {
            width = Screen.FromControl(this).Bounds.Width;
            height = Screen.FromControl(this).Bounds.Height;
            
            if (D_WITH_PP)
                ocr = new OCR();

            CreateSprites();
        }

        // Timer for image refreshing
        private void SetTimer()
        {
            pollTimer = new System.Timers.Timer(updateRate);
            pollTimer.Elapsed += OnTimedEvent;
            pollTimer.AutoReset = true;
            pollTimer.Enabled = true;
        }

        // Triggered every (updateRate) ms
        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            UpdatePP();
            LoadNewPicture();
        }

        // Read window content of PPShow i.e. read the current pp from Sync
        private static void UpdatePP()
        {
            if (D_WITH_PP)
            {
                ocrDelayer++;
                if (ocrDelayer == 50)
                {
                    ocrDelayer = 0;
                    int currPP = GetOcrInt();
                    lastPpValue = currPP == -1 ? 0 : currPP;
                    if (currPP == 0 || currPP == -1)
                        highestPp = 0;
                }
            }
        }

        // Call to ocr, get pp value if read successfull, otherwise get -1
        private static int GetOcrInt()
        {
            // Reading, take all characters before the first '.', remove all non-digits, remove all new lines
            string ocrResult = Regex.Replace(ocr.GetNextText().Split('.')[0], @"[^\d]", "").Replace("\n", "");
            if (int.TryParse(ocrResult, out int ppValue) && ppValue >= 0 && ppValue <= 2000)
                return ppValue;
            else
                return -1;
        }

        // Retrieve cursor position and transform into sector range
        private Tuple<int, int> GetCurrentSector()
        {
            // Map cursor position to sector range
            int xSec = (int)((double)xSectors / width * MousePosition.X);
            int ySec = (int)((double)ySectors / height * MousePosition.Y);

            // If outside of main screen
            if (xSec < 0 || xSec > xSectors - 1 || ySec < 0 || ySec > ySectors - 1)
            {
                xSec = 0;
                ySec = 0;
            }
            return new Tuple<int, int>(xSec, ySec);
        }

        // If little pp or choke: sad (3,4) --- if much pp: happy (0,1)--- else: neutral (2)
        private int AnalyzeMood()
        {
            if (lastPpValue < highestPp * 0.95)
                return 3;   // Sad
            else if (lastPpValue < highestPp * 0.85)
                return 4;   // Very sad
            
            if (lastPpValue > 0 && lastPpValue < 60)
                return 4;    // Very sad
            else if (lastPpValue >= 60 && lastPpValue < 110)
                return 3;   // Sad
            else if (lastPpValue >= 170 && lastPpValue < 220)
                return 1;   // Happy
            else if (lastPpValue >= 220)
                return 0;   // Very happy
            else
                return 2;   // Neutral
        }

        // 0: no tapping --- 1: button1 pressed --- 2: button2 pressed --- 1 or 2 if other button pressed
        private int AnalyzeTapping()
        {
            if (pressedKeys.Count == 1)
            {
                int currPressedKeysHash = GetSequenceHashCode(pressedKeys);
                if (pressedKeys[0] == button1)
                    return 1;               // button1
                else if (pressedKeys[0] == button2)
                    return 2;               // button2
                else if (currPressedKeysHash != lastPressedKeysHash)
                {
                    lastPressedKeysHash = currPressedKeysHash;
                    return rand.Next(1, 3); // 1 or 2 randomly
                }
                else
                    return tapping;         // no change (same button still pressed)
            }
            else if (pressedKeys.Count > 1)
                return tapping;             // no change
            else
                return 0;                   // no tapping
        }

        // Retrieve situation info and select appropriate picture
        private void LoadNewPicture()
        {
            // Retrieve info
            Tuple<int, int> currSectors = D_WITH_CURSOR ? GetCurrentSector() : new Tuple<int, int>(0, 0);
            tapping = D_WITH_TAPPING ? AnalyzeTapping() : tapping;
            int mood = D_WITH_PP ? AnalyzeMood() : 2;

            // Find picture
            int mapKey = GetIntHashCode(currSectors.Item1, currSectors.Item2, tapping, mood);
            if (lastImage == mapKey)    // no unnecessary image retrieving
                return;
            lastImage = mapKey;
            pictureBox.Image = imageMap[mapKey];
        }

        // Given 4 integers, return their hash code
        private static int GetIntHashCode(int i1, int i2, int i3, int i4)
        {
            int hash = 23;
            hash = hash * 31 + i1;
            hash = hash * 31 + i2;
            hash = hash * 31 + i3;
            hash = hash * 31 + i4;
            return hash;
        }

        // Given a list, return its hash code
        private static int GetSequenceHashCode<T>(IList<T> sequence)
        {
            const int seed = 487;
            const int modifier = 31;
            unchecked
            {
                return sequence.Aggregate(seed, (current, item) => (current * modifier) + item.GetHashCode());
            }
        }

        // Draws smallBmp over largeBmp, location depends on x_margin and y_margin
        public static Bitmap Superimpose(Bitmap largeBmp, Bitmap smallBmp, int x_margin, int y_margin)
        {
            Bitmap largeBmpCopy = (Bitmap)largeBmp.Clone();
            using (Graphics g = Graphics.FromImage(largeBmpCopy))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                int x = largeBmpCopy.Width - smallBmp.Width - x_margin;
                int y = largeBmpCopy.Height - smallBmp.Height - y_margin;
                g.DrawImage(smallBmp, new Point(x, y));
                return largeBmpCopy;
            }
        }

        // Create mouth and tapping related bitmaps
        private static void CreateSprites()
        {
            int mouth_x_margin = 110;
            int mouth_y_margin = 165;
            int tap_x_margin = 70;
            int tap_y_margin = 55;

            using (Bitmap b = Properties.Resources.baseSprite)
            {
                using (Bitmap happy2 = Superimpose(b, Properties.Resources.mH2, mouth_x_margin, mouth_y_margin))
                {
                    using (Bitmap tapL = Superimpose(happy2, Properties.Resources.tL, tap_x_margin, tap_y_margin))
                        FillImageMap(tapL, 0, 2);
                    using (Bitmap tapR = Superimpose(happy2, Properties.Resources.tR, tap_x_margin, tap_y_margin))
                        FillImageMap(tapR, 0, 1);
                    using (Bitmap tapU = Superimpose(happy2, Properties.Resources.tU, tap_x_margin, tap_y_margin))
                        FillImageMap(tapU, 0, 0);
                }
                using (Bitmap happy1 = Superimpose(b, Properties.Resources.mH1, mouth_x_margin, mouth_y_margin))
                {
                    using (Bitmap tapL = Superimpose(happy1, Properties.Resources.tL, tap_x_margin, tap_y_margin))
                        FillImageMap(tapL, 1, 2);
                    using (Bitmap tapR = Superimpose(happy1, Properties.Resources.tR, tap_x_margin, tap_y_margin))
                        FillImageMap(tapR, 1, 1);
                    using (Bitmap tapU = Superimpose(happy1, Properties.Resources.tU, tap_x_margin, tap_y_margin))
                        FillImageMap(tapU, 1, 0);
                }
                using (Bitmap neutral = Superimpose(b, Properties.Resources.mN, mouth_x_margin, mouth_y_margin))
                {
                    using (Bitmap tapL = Superimpose(neutral, Properties.Resources.tL, tap_x_margin, tap_y_margin))
                        FillImageMap(tapL, 2, 2);
                    using (Bitmap tapR = Superimpose(neutral, Properties.Resources.tR, tap_x_margin, tap_y_margin))
                        FillImageMap(tapR, 2, 1);
                    using (Bitmap tapU = Superimpose(neutral, Properties.Resources.tU, tap_x_margin, tap_y_margin))
                        FillImageMap(tapU, 2, 0);
                }
                using (Bitmap sad1 = Superimpose(b, Properties.Resources.mS1, mouth_x_margin, mouth_y_margin))
                {
                    using (Bitmap tapL = Superimpose(sad1, Properties.Resources.tL, tap_x_margin, tap_y_margin))
                        FillImageMap(tapL, 3, 2);
                    using (Bitmap tapR = Superimpose(sad1, Properties.Resources.tR, tap_x_margin, tap_y_margin))
                        FillImageMap(tapR, 3, 1);
                    using (Bitmap tapU = Superimpose(sad1, Properties.Resources.tU, tap_x_margin, tap_y_margin))
                        FillImageMap(tapU, 3, 0);
                }
                using (Bitmap sad2 = Superimpose(b, Properties.Resources.mS2, mouth_x_margin, mouth_y_margin))
                {
                    using (Bitmap tapL = Superimpose(sad2, Properties.Resources.tL, tap_x_margin, tap_y_margin))
                        FillImageMap(tapL, 4, 2);
                    using (Bitmap tapR = Superimpose(sad2, Properties.Resources.tR, tap_x_margin, tap_y_margin))
                        FillImageMap(tapR, 4, 1);
                    using (Bitmap tapU = Superimpose(sad2, Properties.Resources.tU, tap_x_margin, tap_y_margin))
                        FillImageMap(tapU, 4, 0);
                }
            }
        }

        // Create cursor related bitmaps and put them in imageMap
        private static void FillImageMap(Bitmap baseSprite, int mouth, int tap)
        {
            int cursor_x_margin = 190;
            int cursor_y_margin = 75;

            // x sector, y sector, tapping (no tapping, button1, button2), mood (very happy, happy, neutral, sad, very sad)
            imageMap.Add(GetIntHashCode(0, 0, tap, mouth), Superimpose(baseSprite, Properties.Resources.c00, cursor_x_margin, cursor_y_margin));
            imageMap.Add(GetIntHashCode(1, 0, tap, mouth), Superimpose(baseSprite, Properties.Resources.c10, cursor_x_margin, cursor_y_margin));
            imageMap.Add(GetIntHashCode(2, 0, tap, mouth), Superimpose(baseSprite, Properties.Resources.c20, cursor_x_margin, cursor_y_margin));
            imageMap.Add(GetIntHashCode(3, 0, tap, mouth), Superimpose(baseSprite, Properties.Resources.c30, cursor_x_margin, cursor_y_margin));
            imageMap.Add(GetIntHashCode(0, 1, tap, mouth), Superimpose(baseSprite, Properties.Resources.c01, cursor_x_margin, cursor_y_margin));
            imageMap.Add(GetIntHashCode(1, 1, tap, mouth), Superimpose(baseSprite, Properties.Resources.c11, cursor_x_margin, cursor_y_margin));
            imageMap.Add(GetIntHashCode(2, 1, tap, mouth), Superimpose(baseSprite, Properties.Resources.c21, cursor_x_margin, cursor_y_margin));
            imageMap.Add(GetIntHashCode(3, 1, tap, mouth), Superimpose(baseSprite, Properties.Resources.c31, cursor_x_margin, cursor_y_margin));
        }
    }
}

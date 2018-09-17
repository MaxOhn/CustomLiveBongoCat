
using System.Timers;
using System.Collections.Generic;
using System.Windows.Forms;
using Utilities;

namespace live_cam
{
    public partial class LiveCam : Form
    {
        private static KeyboardHook hook = new KeyboardHook();
        private static System.Timers.Timer timer;
        private static List<KeyboardHook.VKeys> pressedKeys = new List<KeyboardHook.VKeys>();
        private static int currSecX = 0;
        private static int currSecY = 0;
        private static int xSectors = 4;
        private static int ySectors = 2;
        private static Dictionary<int, System.Drawing.Bitmap> imageMap = new Dictionary<int, System.Drawing.Bitmap>();
        private static int width;
        private static int height;
        private static int lastImage;
        private static int lastTapping;
        private static System.Random rand;

        KeyboardHook.VKeys button1 = KeyboardHook.VKeys.NUMPAD4;
        KeyboardHook.VKeys button2 = KeyboardHook.VKeys.NUMPAD5;

        public LiveCam()
        {
            KeyPreview = true;

            InitializeVariables();
            InitializeComponent();
            SetTimer();

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

        private int HashInts(int i1, int i2, int i3)
        {
            int hash = 23;
            hash = hash * 31 + i1;
            hash = hash * 31 + i2;
            hash = hash * 31 + i3;
            return hash;
        }

        private void InitializeVariables()
        {
            width = Screen.FromControl(this).Bounds.Width;
            height = Screen.FromControl(this).Bounds.Height;

            lastImage = -1;     // Anything but a result from HashInts function
            lastTapping = 0;    // no tapping

            rand = new System.Random();

            // x sector, y sector, tapping (no tapping, button1, button2)
            imageMap.Add(HashInts(0, 0, 0), Properties.Resources.c00_tU);
            imageMap.Add(HashInts(1, 0, 0), Properties.Resources.c10_tU);
            imageMap.Add(HashInts(2, 0, 0), Properties.Resources.c20_tU);
            imageMap.Add(HashInts(3, 0, 0), Properties.Resources.c30_tU);
            imageMap.Add(HashInts(0, 1, 0), Properties.Resources.c01_tU);
            imageMap.Add(HashInts(1, 1, 0), Properties.Resources.c11_tU);
            imageMap.Add(HashInts(2, 1, 0), Properties.Resources.c21_tU);
            imageMap.Add(HashInts(3, 1, 0), Properties.Resources.c31_tU);
            imageMap.Add(HashInts(0, 0, 1), Properties.Resources.c00_tR);
            imageMap.Add(HashInts(1, 0, 1), Properties.Resources.c10_tR);
            imageMap.Add(HashInts(2, 0, 1), Properties.Resources.c20_tR);
            imageMap.Add(HashInts(3, 0, 1), Properties.Resources.c30_tR);
            imageMap.Add(HashInts(0, 1, 1), Properties.Resources.c01_tR);
            imageMap.Add(HashInts(1, 1, 1), Properties.Resources.c11_tR);
            imageMap.Add(HashInts(2, 1, 1), Properties.Resources.c21_tR);
            imageMap.Add(HashInts(3, 1, 1), Properties.Resources.c31_tR);
            imageMap.Add(HashInts(0, 0, 2), Properties.Resources.c00_tL);
            imageMap.Add(HashInts(1, 0, 2), Properties.Resources.c10_tL);
            imageMap.Add(HashInts(2, 0, 2), Properties.Resources.c20_tL);
            imageMap.Add(HashInts(3, 0, 2), Properties.Resources.c30_tL);
            imageMap.Add(HashInts(0, 1, 2), Properties.Resources.c01_tL);
            imageMap.Add(HashInts(1, 1, 2), Properties.Resources.c11_tL);
            imageMap.Add(HashInts(2, 1, 2), Properties.Resources.c21_tL);
            imageMap.Add(HashInts(3, 1, 2), Properties.Resources.c31_tL);
        }

        private void SetTimer()
        {
            timer = new System.Timers.Timer(50);
            timer.Elapsed += OnTimedEvent;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            SetCurrentSector();
            LoadNewPicture();
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

        private int AnalyzeTapping()
        {
            // 0: no tapping --- 1: button1 --- 2: button2 --- 1 or 2 if other button
            int tapping = 0;
            if (pressedKeys.Count == 1)
            {
                if (pressedKeys[0] == button1)
                {
                    tapping = 1;
                }
                else if (pressedKeys[0] == button2)
                {
                    tapping = 2;
                }
                else
                {
                    tapping = rand.Next(1, 3);
                }
            }
            else if (pressedKeys.Count > 1)
            {
                tapping = lastTapping;
            }
            return tapping;
        }

        private void LoadNewPicture()
        {
            lastTapping = AnalyzeTapping();
            int mapKey = HashInts(currSecX, currSecY, lastTapping);
            if (lastImage == mapKey)    // no unnecessary image retrieving
                return;
            lastImage = mapKey;
            pictureBox1.Image = imageMap[mapKey];
        }
    }
}

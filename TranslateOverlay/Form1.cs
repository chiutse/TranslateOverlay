using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace TranslateOverlay
{
    public partial class Form1 : Form
    {
        PictureBox pb;
        Bitmap bitmap;

        List<Control> objects;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static Form1 instance;

        public string OCR_API_KEY = "";
        public string TRANSLATE_API_KEY = "";
        public string language = "";

        public const string CONFIG_DATA_PATH = "./config.json";

        /* hook event */
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public Form1()
        {
            InitializeComponent();
            instance = this;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            objects = new List<Control>();
            _hookID = SetHook(_proc);
            LoadConfig();
        }

        public void LoadConfig()
        {
            if(System.IO.File.Exists(CONFIG_DATA_PATH))
            {
                try
                {
                    string data = System.IO.File.ReadAllText(CONFIG_DATA_PATH);                   
                    TranslateOverlayConfig config = JsonConvert.DeserializeObject<TranslateOverlayConfig>(data);
                    OCR_API_KEY = config.ocrAPIKey;
                    TRANSLATE_API_KEY = config.translateAPIKey;
                    language = config.language;
                    ocrKeyTextbox.Text = OCR_API_KEY;
                    translateKeyTextBox.Text = TRANSLATE_API_KEY;
                    languageTextbox.Text = language;
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                    MessageBox.Show(e.Message);
                }
            }
        }

        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                //Console.WriteLine((Keys)vkCode);
                instance.HotKeyDown((Keys)vkCode);
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        public void HotKeyDown(Keys key)
        {
            // focus target window
            UpdateTargetScreenSize();

            if (key == Keys.F1 || key == Keys.F5)
            {
                CaptureScreenAndOCR(false);
            }

            if (key == Keys.F2 || key == Keys.F6)
            {
                CaptureScreenAndOCR(true);
            }

            if (key == Keys.F3 || key == Keys.F7)
            {
                SettingsPanelToggle();
            }

            if (key == Keys.F4 || key == Keys.F8)
            {
                ClearAllObjects();
            }
        }

        void SettingsPanelToggle()
        {
            settingsPanel.Visible = !(settingsPanel.Visible);
        }

        void UpdateTargetScreenSize()
        {
            Console.WriteLine("Focus on " + GetActiveWindowTitle());
            // try to change size 
            IntPtr handle = GetForegroundWindow();
            Rect targetRect = new Rect();
            GetWindowRect(handle, ref targetRect);
            this.Left = targetRect.Left;
            this.Top = targetRect.Top;
            this.Size = new Size(targetRect.Right - this.Left, targetRect.Bottom - this.Top);
            this.TopMost = true;
            //this.Activate();
        }

        /* OCR part */
        public void CaptureScreenAndOCR(bool byLine)
        {
            Rectangle bounds = this.Bounds;
            bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                Rectangle screenRectangle = RectangleToScreen(this.ClientRectangle);
                int titleHeight = screenRectangle.Top - this.Top;
                int borderWidth = screenRectangle.Left;
                g.CopyFromScreen(new Point(screenRectangle.Left, bounds.Top + titleHeight), Point.Empty, bounds.Size);

                OCRRequestAsync(byLine);
            }            
        }

        public async System.Threading.Tasks.Task OCRRequestAsync(bool byLine)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                httpClient.Timeout = new TimeSpan(1, 1, 1);

                MultipartFormDataContent form = new MultipartFormDataContent();
                Console.WriteLine("OCRKEY =" + OCR_API_KEY);
                form.Add(new StringContent(OCR_API_KEY), "apikey");

                // to jpeg for reduce size
                MemoryStream ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Jpeg);
                byte[] data = ms.ToArray();

                form.Add(new ByteArrayContent(data, 0, data.Length), "image", "image.jpg");
                form.Add(new StringContent("eng"), "language");
                form.Add(new StringContent("true"), "isOverlayRequired");

                HttpResponseMessage response = await httpClient.PostAsync("https://api.ocr.space/Parse/Image", form);

                string strContent = await response.Content.ReadAsStringAsync();
                System.Console.WriteLine(strContent);

                Rootobject ocrResult = JsonConvert.DeserializeObject<Rootobject>(strContent);

                ClearAllObjects();

                string resultTxt = "";
                if (ocrResult.OCRExitCode == 1)
                {
                    for (int i = 0; i < ocrResult.ParsedResults.Count(); i++)
                    {
                        resultTxt = resultTxt + ocrResult.ParsedResults[i].ParsedText;
                        List<Line> lines = ocrResult.ParsedResults[i].TextOverlay.Lines;
                        for (int j = 0; j < lines.Count; j++)
                        {
                            Line line = lines[j];
                            if (byLine)
                            {
                                string lineText = "";
                                Word startWord = line.Words[0];
                                int totalWidth = 0;
                                for (int k = 0; k < line.Words.Count; k++)
                                {
                                    Word word = line.Words[k];
                                    if (k < line.Words.Count - 1)
                                        lineText += word.WordText + " ";
                                    else
                                        lineText += word.WordText;
                                    totalWidth += (int)word.Width;
                                }
                                CreateTextObject(lineText, new Point((int)startWord.Left, (int)startWord.Top), new Size(totalWidth, (int)line.MaxHeight));
                            }
                            else
                            {
                                for (int k = 0; k < line.Words.Count; k++)
                                {
                                    Word word = line.Words[k];
                                    CreateTextObject(word.WordText, new Point((int)word.Left, (int)word.Top), new Size((int)word.Width, (int)word.Height));
                                }
                            }
                        }
                    }
                }
                else
                {
                    System.Console.WriteLine("ERROR: " + strContent);
                }
                System.Console.WriteLine("[Result] " + resultTxt);
            }
            catch (Exception exception)
            {
                System.Console.WriteLine("ERROR: " + exception.Message);
            }
        }

        /* display result to overlay */
        public async System.Threading.Tasks.Task CreateTextObject(string msg, Point point, Size size)
        {
            try
            {
                HttpClient client = new HttpClient();

                // using yandex translate                
                string url = "https://translate.yandex.net/api/v1.5/tr.json/translate?key=" + TRANSLATE_API_KEY + "&text=" + msg + "&lang=" + language;
                var responseString = await client.GetStringAsync(url);

                TranslateObject result = JsonConvert.DeserializeObject<TranslateObject>(responseString);

                string output = "";
                for(int i=0; i<result.text.Count; i++)
                {
                    output += result.text[i];
                }
                
                // using google translate
                //string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=zh-TW&dt=t&q=" + msg;
                //var responseString = await client.GetStringAsync(url);
                //Console.WriteLine(responseString);
                // pick text from between first " and second "
                //string output = responseString.Split('\"')[1];

                Label label = new Label();
                label.Text = output;
                label.Location = point;
                label.Size = size;
                label.BackColor = Color.Black;
                label.ForeColor = Color.White;

                objects.Add(label);
                this.Controls.Add(label);

                client.Dispose();
            }
            catch (Exception exception)
            {
                //MessageBox.Show("Ooops");
                System.Console.WriteLine("ERROR: " + exception.Message);
            }
}

        public void ClearAllObjects()
        {
            for(int i=0; i<objects.Count; i++)
            {
                this.Controls.Remove(objects[i]);
                objects[i].Dispose();
            }
            objects.Clear();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://tech.yandex.com/translate/doc/dg/concepts/api-overview-docpage/#api-overview__languages");
        }

        private void hideButton_Click(object sender, EventArgs e)
        {
            SettingsPanelToggle();
        }

        private void translateKeyTextBox_TextChanged(object sender, EventArgs e)
        {
            TRANSLATE_API_KEY = translateKeyTextBox.Text;
        }

        private void ocrKeyTextbox_TextChanged(object sender, EventArgs e)
        {
            OCR_API_KEY = ocrKeyTextbox.Text;
        }

        private void languageTextbox_TextChanged(object sender, EventArgs e)
        {
            language = languageTextbox.Text;
        }

        // save to json
        public void SaveConfig()
        {
            try
            {
                TranslateOverlayConfig config = new TranslateOverlayConfig();
                config.translateAPIKey = TRANSLATE_API_KEY;
                config.ocrAPIKey = OCR_API_KEY;
                config.language = language;
                string data = JsonConvert.SerializeObject(config);
                System.IO.File.WriteAllText(CONFIG_DATA_PATH, data);
            }catch(Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            SaveConfig();
        }
    }
}

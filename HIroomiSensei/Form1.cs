using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using Tesseract;
using OpenCvSharp;
using Google.Cloud.Translation.V2;
using Google.Apis.Auth.OAuth2;

namespace HIroomiSensei
{
    
    public struct Rect
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

    }


    public partial class Form1 : Form
    {
        private readonly string imgTemp = "temp.png";
        private readonly string[] LangCodeForGoogle = { "en", "ja", "ko" };
        private readonly string[] lstExcept = { "Runtime Broker", "svchost", "conhost" };

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

        public Form1()
        {
            InitializeComponent();
        }

        private void cbWindowsList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(cbWindowsList.SelectedIndex >= 0)
            {
                // 프로세스 이름 가져오기
                string s = cbWindowsList.SelectedItem as string;

                // 이름 기반으로 프로세스 찾기
                Process[] lstProc = Process.GetProcessesByName(s);

                // 항상 유일한 프로세스 일것이라고 가정
                Process gameProc = lstProc[0]; 

                IntPtr ipMainWindow = gameProc.MainWindowHandle;
                Rect gameRect = new Rect();
                GetWindowRect(ipMainWindow, ref gameRect);

                txtX.Text = gameRect.Left.ToString();
                txtY.Text = gameRect.Top.ToString();
                txtH.Text = (gameRect.Bottom - gameRect.Top).ToString();
                txtW.Text = (gameRect.Right - gameRect.Left).ToString();


            }
        }

        private void cbWindowsList_DropDown(object sender, System.EventArgs e)
        {
            // 프로세스 목록 업데이트
            Process[] allProc = Process.GetProcesses();
            foreach (Process p in allProc)
            {
                if (Array.IndexOf(lstExcept, p.ProcessName) < 0)
                {
                    cbWindowsList.Items.Add(p.ProcessName);
                }
                
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // 사실 상 메인 루틴
            if (cbWindowsList.SelectedIndex >= 0 && cbLang.SelectedIndex >= 0)
            {
                
                CaptureImage();
                ImgPreprocess();
                GetTextFromBMP();
                TranslateText();

                
                if (File.Exists(imgTemp))
                {
                    File.Delete(imgTemp);
                }

            }
                
        }

        public void CaptureImage()

        {

            try
            {
                // txtbox에 있는 값 가져오기
                int x = Int32.Parse(txtX.Text);
                int y = Int32.Parse(txtY.Text);
                int w = Int32.Parse(txtW.Text);
                int h = Int32.Parse(txtH.Text);

                Rectangle rect = new Rectangle(x, y, w, h);

                // 픽셀 포맷 정보 얻기 (Optional)
                int bitsPerPixel = Screen.PrimaryScreen.BitsPerPixel;
                PixelFormat pixelFormat = PixelFormat.Format32bppArgb;
                if (bitsPerPixel <= 16)
                {
                    pixelFormat = PixelFormat.Format16bppRgb565;
                }
                if (bitsPerPixel == 24)
                {
                    pixelFormat = PixelFormat.Format24bppRgb;
                }

                // 화면 크기만큼의 Bitmap 생성
                Bitmap bmp = new Bitmap(rect.Width, rect.Height, pixelFormat);

                // Bitmap 이미지 변경을 위해 Graphics 객체 생성
                using (Graphics gr = Graphics.FromImage(bmp))
                {
                    // 화면을 그대로 카피해서 Bitmap 메모리에 저장
                    gr.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size);
                }

                


                // Bitmap 데이타를 파일로 저장
                bmp.Save(imgTemp, System.Drawing.Imaging.ImageFormat.Png);

                // 메모리 해제
                bmp.Dispose();

            }

            catch (Exception e)
            {
                MessageBox.Show($"{e.GetType()}\n{e.Message}", "CaptureImage");
            }

        }

        public void ImgPreprocess()
        {
            // 이미지 객체 생성
            var img = Cv2.ImRead(imgTemp);
            var dst = new Mat(img.Size(), MatType.CV_8UC1);

            // temp.png 수정
            Cv2.CvtColor(img, dst, ColorConversionCodes.BGR2GRAY);
            Cv2.ImWrite(imgTemp, dst);

            // 테스트용 출력
            // Cv2.ImShow("Image", dst);

            img.Dispose();
            dst.Dispose();
        }


        public void GetTextFromBMP()
        {
            try 
            {
                // 이미지 가져오기
                Bitmap img = new Bitmap(imgTemp);

                // 언어 설정 가져오기
                string lang = cbLang.SelectedItem as string;

                // OCR엔진 생성
                var ocr = new TesseractEngine($"tessdata/{lang}", lang, EngineMode.TesseractAndLstm);

                // OCR처리
                var texts = ocr.Process(img);

                // 출력
                //MessageBox.Show(texts.GetText());
                string str_for_replace = texts.GetText();
                str_for_replace.Replace("\n\n", "");
                txtSource.Text = str_for_replace;

                //메모리 해제
                img.Dispose();
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.GetType()}\n{e.Message}", "GetTextFromBMP");
            }

        }

        public void TranslateText()
        {
            int lang = cbLang.SelectedIndex;
            string srcText = txtSource.Text;

            var credential = GoogleCredential.FromFile("HiroomiSensei.json");
            TranslationClient client = TranslationClient.Create(credential);
            var response = client.TranslateText(text: srcText, targetLanguage: LangCodeForGoogle[2], sourceLanguage: LangCodeForGoogle[lang]);
            txtResult.Text = response.TranslatedText;
        }
    }
}
